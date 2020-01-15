module AkklingHelpers

open Akka.Actor
open Akka.Cluster.Sharding
open Akkling
open Akkling.Persistence
open Akkling.Cluster.Sharding


type Extractor<'Envelope, 'Message> = 'Envelope -> string*string*'Message
type ShardResolver = string -> string

type internal TypedMessageExtractor<'Envelope, 'Message>(extractor: Extractor<_,'Message>, shardResolver :ShardResolver) =
    interface IMessageExtractor with
        member _.ShardId message =
            match message with
            | :? 'Envelope as env ->
                let shardId, _, _ = extractor env
                shardId
            | :? ShardRegion.StartEntity as e -> shardResolver (e.EntityId)
            | _ -> invalidOp <| message.ToString()
        member _.EntityId message =
            match message with
            | :? 'Envelope as env ->
                let _, entityId, _ = extractor env
                entityId
            | other -> invalidOp <| string other
        member _.EntityMessage message =
            match message with
            | :? 'Envelope as env ->
                let _, _, msg = extractor env
                box msg
            | other -> invalidOp <| string other


// HACK over persistent actors
type FunPersistentShardingActor<'Message>(actor : Eventsourced<'Message> -> Effect<'Message>) as this =
    inherit FunPersistentActor<'Message>(actor)
    // sharded actors are produced in path like /user/{name}/{shardId}/{entityId}, therefore "{name}/{shardId}/{entityId}" is peristenceId of an actor
    let pid = this.Self.Path.Parent.Parent.Name + "/" + this.Self.Path.Parent.Name + "/" + this.Self.Path.Name
    override _.PersistenceId = pid

// this function hacks persistent functional actors props by replacing them with dedicated sharded version using different PeristenceId strategy
let internal adjustPersistentProps (props: Props<'Message>) : Props<'Message> =
    if props.ActorType = typeof<FunPersistentActor<'Message>>
    then { props with ActorType = typeof<FunPersistentShardingActor<'Message>> }
    else props


let entityFactoryFor (system: ActorSystem) (shardResolver:ShardResolver) (name: string) (props: Props<'Message>) (rememberEntities) : EntityFac<'Message> =

    let clusterSharding = ClusterSharding.Get(system)
    let adjustedProps = adjustPersistentProps props
    let shardSettings =
        match rememberEntities with
        | true -> ClusterShardingSettings.Create(system).WithRememberEntities(true)
        | _ -> ClusterShardingSettings.Create(system);
    let shardRegion =
        clusterSharding.Start(name, adjustedProps.ToProps(),
            shardSettings, new TypedMessageExtractor<_,_>(EntityRefs.entityRefExtractor, shardResolver))
    { ShardRegion = shardRegion; TypeName = name }

let (|Recovering|_|) (context: Eventsourced<'Message>) (msg: 'Message) : 'Message option =
    if context.IsRecovering ()
    then Some msg
    else None

