module Common

open System
open Akkling
open Akkling.Persistence
open Akka.Cluster.Tools.PublishSubscribe
open Akka
open Akka.Cluster.Sharding
open Akkling.Cluster.Sharding
open Akka.Actor
open Akka.Serialization
open System.IO
open Newtonsoft.Json
open System.Text
open NodaTime
open NodaTime.Serialization.JsonNet

type PlainNewtonsoftJsonSerializer(system: ExtendedActorSystem) =
    inherit Serializer(system)

    let ser = new JsonSerializer()

    do
        ser.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        |> ignore

    override __.IncludeManifest = true

    override __.Identifier = 1711234423

    override __.ToBinary(o) =
        ser.TypeNameHandling <- TypeNameHandling.All
        ser.MetadataPropertyHandling <- MetadataPropertyHandling.ReadAhead

        let memoryStream = new MemoryStream()

        use streamWriter =
            new StreamWriter(memoryStream, Encoding.UTF8)

        ser.Serialize(streamWriter, o, o.GetType())
        streamWriter.Flush()
        memoryStream.ToArray()

    override __.FromBinary(bytes, ttype) =
        ser.TypeNameHandling <- TypeNameHandling.All
        ser.MetadataPropertyHandling <- MetadataPropertyHandling.ReadAhead

        use streamReader =
            new StreamReader(new MemoryStream(bytes), Encoding.UTF8)

        ser.Deserialize(streamReader, ttype)


type Command<'Command> =
    {
        Command: 'Command
        CreationDate: Instant
        CorrelationId: string
    }

type Event<'Event> =
    {
        Event: 'Event
        CreationDate: Instant
        CorrelationId: string
        Version: int
    }


let toEvent (clockInstance: IClock) ci event version =
    {
        Event = event
        CreationDate = clockInstance.GetCurrentInstant()
        CorrelationId = ci
        Version = version
    }

type IDefaultTag =
    interface
    end

type Message<'Command, 'Event> =
    | Command of Command<'Command>
    | Event of Event<'Event>
    interface IDefaultTag

module SagaStarter =
    let removeSaga (name: string) =
        let first = name.Replace("_Saga_", "_")
        let index = first.IndexOf('_')
        let lastIndex = first.LastIndexOf('_')
        if index <> lastIndex then first.Substring(index + 1) else first

    let toOriginatorName (name: string) =
        let sagaRemoved = removeSaga name
        let bang = sagaRemoved.IndexOf('~')
        sagaRemoved.Substring(0, bang)

    let toRawGuid name =
        let originatorName = name |> toOriginatorName
        let index = originatorName.IndexOf('_')
        originatorName.Substring(index + 1)

    let toCid name = name + "~" + Guid.NewGuid().ToString()

    let cidToSagaName (name: string) = name.Replace("_", "_Saga_")
    let isSaga (name: string) = name.Contains("_Saga_")

    [<Literal>]
    let SagaStarterName = "SagaStarter"

    [<Literal>]
    let SagaStarterPath = "/user/SagaStarter"


    type Command =
        | CheckSagas of obj * originator: Actor.IActorRef * cid: string
        | Continue

    type Event = SagaCheckDone

    type Message =
        | Command of Command
        | Event of Event

    let toCheckSagas (event, originator, cid) =
        ((event |> box), originator, cid)
        |> CheckSagas
        |> Command

    let toSendMessage mediator (originator) event cid =
        let message =
            Send(SagaStarterPath, (event, untyped originator, cid) |> toCheckSagas)

        (mediator <? (message))
        |> Async.RunSynchronously
        |> ignore

    let publishEvent (mailbox: Actor<_>) (mediator) event =
        let sender = mailbox.Sender()
        let self = mailbox.Self

        if sender.Path.Name |> isSaga then
            let originatorName = sender.Path.Name |> toOriginatorName

            if originatorName <> self.Path.Name
            then mediator <! Publish(originatorName, event)

        mediator <! Publish(self.Path.Name, event)

    let cont (mediator) =
        mediator
        <! box (Send(SagaStarterPath, Continue |> Command))



    let subscriber (mediator: IActorRef<_>) (mailbox: Eventsourced<_>) =
        let originatorName =
            mailbox.Self.Path.Name |> toOriginatorName

        mediator
        <! box (Subscribe(originatorName, untyped mailbox.Self))

    let (|SubscrptionAcknowledged|_|) (context: Actor<obj>) (msg: obj): obj option =
        let originatorName =
            context.Self.Path.Name |> toOriginatorName

        match msg with
        | :? SubscribeAck as s when s.Subscribe.Topic = originatorName -> Some msg
        | _ -> None

    let actorProp (sagaCheck: obj -> (((string -> IEntityRef<_>) * string) list)) (mailbox: Actor<_>) =
        let rec set (state: Map<string, (Actor.IActorRef * string list)>) =

            let startSaga cid (originator: Actor.IActorRef) (list: ((string -> IEntityRef<_>) * string) list) =
                let sender = untyped <| mailbox.Sender()

                let sagas =
                    [
                        for (factory, prefix) in list do
                            let saga =
                                cid
                                |> cidToSagaName
                                |> fun name ->
                                    match prefix with
                                    | null
                                    | "" -> name
                                    | other -> sprintf "%s_%s" other name
                                |> factory

                            saga
                            <! box (ShardRegion.StartEntity(saga.EntityId))

                            yield saga.EntityId
                    ]

                let name = originator.Path.Name

                let state =
                    match state.TryFind(name) with
                    | None -> state.Add(name, (sender, sagas))
                    | Some (_, list) -> state.Remove(name).Add(name, (sender, list @ sagas))

                state

            actor {
                match! mailbox.Receive() with
                | Command (Continue) ->
                    //check if all sagas are started. if so issue SagaCheckDone to originator else keep wait
                    let sender = untyped <| mailbox.Sender()
                    let originName = sender.Path.Name |> toOriginatorName
                    //weird bug cause an NRE with TryGet
                    let matchFound = state.ContainsKey(originName)

                    if not matchFound then
                        return! set state
                    else
                        let (originator, subscribers) = state.[originName]

                        let newList =
                            subscribers
                            |> List.filter (fun a -> a <> sender.Path.Name)

                        match newList with
                        | [] -> originator.Tell(SagaCheckDone, untyped mailbox.Self)
                        | _ ->
                            return!
                                set
                                <| state.Remove(originName).Add(originName, (originator, newList))


                | Command (CheckSagas (o, originator, cid)) ->
                    match sagaCheck o with
                    | [] ->
                        mailbox.Sender() <! SagaCheckDone
                        return! set state
                    | list -> return! set <| startSaga cid originator list

                | _ -> return! Unhandled

            }

        set Map.empty

    let init system mediator sagaCheck =
        let sagaStarter =
            spawn system
            <| SagaStarterName
            <| props (actorProp sagaCheck)

        typed mediator <! (sagaStarter |> untyped |> Put)

module CommandHandler =

    let (|SubscriptionAcknowledged|_|) (msg: obj) =
        match msg with
        | :? SubscribeAck as s -> Some s
        | _ -> None

    type CommandDetails<'Command, 'Event> =
        {
            EntityRef: IEntityRef<Message<'Command, 'Event>>
            Cmd: Command<'Command>
            Filter: ('Event -> bool)
        }

    type State<'Command, 'Event> =
        {
            CommandDetails: CommandDetails<'Command, 'Event>
            Sender: IActorRef
        }

    type Command<'Command, 'Event> = Execute of CommandDetails<'Command, 'Event>

    let subscribeForCommand<'Command, 'Event> system mediator (command: Command<'Command, 'Event>) =
        let actorProp mediator (mailbox: Actor<obj>) =
            let rec set (state: State<'Command, 'Event> option) =
                actor {
                    let! msg = mailbox.Receive()

                    match box msg with
                    | SubscriptionAcknowledged _ ->
                        state.Value.CommandDetails.EntityRef
                        <! (state.Value.CommandDetails.Cmd |> Message.Command)

                        return! set state
                    | :? Command<'Command, 'Event> as s ->
                        let sender = mailbox.Sender()

                        let cd =
                            match s with
                            | Execute cd ->
                                monitor mailbox sender |> ignore

                                mediator
                                <! box (Subscribe(cd.EntityRef.EntityId, untyped mailbox.Self))

                                cd

                        return!
                            set
                                (Some
                                    {
                                        CommandDetails = cd
                                        Sender = untyped sender
                                    })
                    | :? (Event<'Event>) as e when e.CorrelationId = state.Value.CommandDetails.Cmd.CorrelationId
                                                   && state.Value.CommandDetails.Filter e.Event ->
                        state.Value.Sender.Tell e
                        return! Stop
                    | Terminated s -> return! Stop
                    | LifecycleEvent _ -> return! Ignore
                    | _ -> return! Unhandled
                }

            set None

        async {
            let! res =
                spawnAnonymous system (props (actorProp mediator))
                <? box command

            return box res :?> Event<'Event>
        }

module QuotationHelpers =

    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.DerivedPatterns

    open ExprShape
    open System.Linq.Expressions
    open System
    open Microsoft.FSharp.Linq.RuntimeHelpers

    let subst newType expression =
        let newVar name = Var.Global(name, newType)

        let rec substituteExpr expression =
            match expression with
            | Call (Some (ShapeVar var), mi, other) ->
                Expr.Call(Expr.Var(newVar var.Name), newType.GetMethod(mi.Name), other)
            | PropertyGet (Some (ShapeVar var), pi, _) ->
                Expr.PropertyGet(Expr.Var(newVar var.Name), newType.GetProperty(pi.Name), [])
            | ShapeVar var -> Expr.Var <| newVar var.Name
            | ShapeLambda (var, expr) -> Expr.Lambda(newVar var.Name, substituteExpr expr)
            | ShapeCombination (shapeComboObject, exprList) ->
                RebuildShapeCombination(shapeComboObject, List.map substituteExpr exprList)

        substituteExpr expression


    let toLinq (expr: Expr<'a -> 'b>) =
        let linq =
            LeafExpressionConverter.QuotationToExpression expr

        let call = linq :?> MethodCallExpression
        let lambda = call.Arguments.[0] :?> LambdaExpression
        Expression.Lambda<Func<'a, 'b>>(lambda.Body, lambda.Parameters)
