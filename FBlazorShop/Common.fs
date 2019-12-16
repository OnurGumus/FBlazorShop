module Common
open System
open Akkling
open Akkling.Persistence
open Akka.Cluster.Tools.PublishSubscribe
open Akka
open Akka.Cluster.Sharding
open Akkling.Cluster.Sharding

type Command<'Command> = {
    Command : 'Command;
    CreationDate  : DateTime;
    CorrelationId : string option
}

type Event<'Event> = {
    Event : 'Event;
    CreationDate  : DateTime;
    CorrelationId : string option
}
with static member toEvent ci event  = {
        Event = event
        CreationDate = DateTime.Now;
        CorrelationId =  ci
    }

type IDefaultTag = interface end

type Message<'Command,'Event> =
    | Command of Command<'Command>
    | Event of Event<'Event>
    with interface IDefaultTag

module SagaStarter =
    let toOriginatorName (name : string) =
        let first = name.Replace("_Saga_","_")
        let index = first.IndexOf('_')
        let lastIndex = first.LastIndexOf('_')
        if index <> lastIndex then
            first.Substring(index + 1)
        else
            first
    let toRawoGuid name =
        let originatorName = name |> toOriginatorName
        let index = originatorName.IndexOf('_')
        originatorName.Substring(index + 1)

    let toSagaName (name : string) = name.Replace("_","_Saga_")
    let isSaga (name : string) = name.Contains("_Saga_")

    [<Literal>]
    let SagaStarterName = "SagaStarter"

    [<Literal>]
    let SagaStarterPath = "/user/SagaStarter"


    type Command  =
        | CheckSagas of obj * originator : Actor.IActorRef
        | Continue
    type Event = SagaCheckDone
    type Message =
          | Command of Command
          | Event of Event

    let toCheckSagas (event, originator) =
        ((event |> box), originator)
            |> CheckSagas |> Command

    let toSendMessage  mediator (originator : IActorRef<_>) event  =
        let message = Send(SagaStarterPath, (event, untyped originator) |> toCheckSagas)
        (mediator <? (message |> box)) |> Async.RunSynchronously |> function
        | SagaCheckDone -> ()


    let publishEvent (mailbox : Eventsourced<_>) (mediator:IActorRef<_>) event=
        let sender = mailbox.Sender()
        let self = mailbox.Self
        if sender.Path.Name |> isSaga then
            let originatorName = sender.Path.Name |> toOriginatorName
            if originatorName <> self.Path.Name then
                mediator <! box (Publish(originatorName, event ))
        else
            sender <! event
        mediator <! box (Publish(self.Path.Name, event))
    let cont (mediator : IActorRef<_>) =
        mediator <! box (Send(SagaStarterPath, Continue |> Command))

    let subscriber (mediator : IActorRef<_>) (mailbox : Eventsourced<_>) =
        let originatorName = mailbox.Self.Path.Name |> toOriginatorName
        mediator <! box (Subscribe(originatorName, untyped mailbox.Self))

    let (|SubscrptionAcknowledged|_|) (context: Eventsourced<obj>) (msg: obj) : obj option =
        let originatorName = context.Self.Path.Name |> toOriginatorName
        match msg with
        | :? SubscribeAck as s when s.Subscribe.Topic = originatorName -> Some msg
        | _ -> None

    let actorProp (sagaCheck : obj -> ((string -> IEntityRef<_>) option)) (mailbox : Actor<_>)=
        let rec set (state  : Map<string, (Actor.IActorRef * string list)>) =

            let startSaga (originator : Actor.IActorRef) (factory : string -> IEntityRef<_>) =
                let sender = untyped <| mailbox.Sender()
                let saga = originator.Path.Name |> toSagaName |> factory
                let state = state.Add(originator.Path.Name,(sender,[saga.EntityId]))
                saga <! box(ShardRegion.StartEntity (saga.EntityId))
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
                    let (originator,subscribers) = state.[originName]
                    let newList = subscribers |> List.filter (fun a -> a <> sender.Path.Name)
                    match newList with
                        | [] -> originator.Tell(SagaCheckDone, untyped mailbox.Self)
                        | _ ->
                            return! set
                                <| state
                                    .Remove(originName)
                                    .Add(originName, (originator,newList))


                | Command(CheckSagas (o, originator)) ->
                    match sagaCheck o with
                    | Some factory ->  return! set <| startSaga originator factory
                    | _ ->
                        mailbox.Sender() <! SagaCheckDone
                        return! set state
                | _ -> return! Unhandled

            }
        set Map.empty

    let init system mediator sagaCheck =
        let sagaStarter =
            spawn system
                <| SagaStarterName
                <| props (actorProp sagaCheck)

        typed mediator <! (sagaStarter |> untyped |> Put)


