module AkklingHelpers

open System
open Akka.Actor
open Akka.Configuration
open Akka.Cluster
open Akka.Cluster.Tools.Singleton
open Akka.Cluster.Sharding
open Akka.Persistence
open Akka.Persistence.Sqlite

open Akkling
open Akkling.Persistence
open Akkling.Cluster
open Akkling.Cluster.Sharding
open Hyperion
open Akka.Serialization
open Akka.Cluster.Tools.PublishSubscribe

[<Literal>]
let DEFAULT_SHARD = "default-shard"

type internal TypedMessageExtractor<'Envelope, 'Message>(extractor: 'Envelope -> string*string*'Message) =
    interface IMessageExtractor with
        member this.ShardId message =
            match message with
            | :? 'Envelope as env ->
                let shardId, _, _ = (extractor(env))
                shardId
            | :? Akka.Cluster.Sharding.ShardRegion.StartEntity as se -> printfn "%A" se.EntityId; DEFAULT_SHARD
            | _ -> invalidOp <| message.ToString()
        member this.EntityId message =
            match message with
            | :? 'Envelope as env ->
                let _, entityId, _ = (extractor(env))
                entityId
            | _ ->printfn "kkj"; "entity-1"
        member this.EntityMessage message =
            match message with
            | :? 'Envelope as env ->
                let _, _, msg = (extractor(env))
                box msg
            | _ -> null


// HACK over persistent actors
type FunPersistentShardingActor<'Message>(actor : Eventsourced<'Message> -> Effect<'Message>) as this =
    inherit FunPersistentActor<'Message>(actor)
    // sharded actors are produced in path like /user/{name}/{shardId}/{entityId}, therefore "{name}/{shardId}/{entityId}" is peristenceId of an actor
    let pid = this.Self.Path.Parent.Parent.Name + "/" + this.Self.Path.Parent.Name + "/" + this.Self.Path.Name
    override this.PersistenceId = pid

// this function hacks persistent functional actors props by replacing them with dedicated sharded version using different PeristenceId strategy
let internal adjustPersistentProps (props: Props<'Message>) : Props<'Message> =
    if props.ActorType = typeof<FunPersistentActor<'Message>>
    then { props with ActorType = typeof<FunPersistentShardingActor<'Message>> }
    else props


let entityFactoryFor (system: ActorSystem) (name: string) (props: Props<'Message>) (rememberEntities) : EntityFac<'Message> =

    let clusterSharding = ClusterSharding.Get(system)
    let adjustedProps = adjustPersistentProps props
    let shardSettings =
        match rememberEntities with
        | true -> ClusterShardingSettings.Create(system).WithRememberEntities(true)
        | _ -> ClusterShardingSettings.Create(system);
    let shardRegion =
        clusterSharding.Start(name, adjustedProps.ToProps(),
            shardSettings, new TypedMessageExtractor<_,_>(EntityRefs.entityRefExtractor))
    { ShardRegion = shardRegion; TypeName = name }

let (|Recovering|_|) (context: Eventsourced<'Message>) (msg: 'Message) : 'Message option =
    if context.IsRecovering ()
    then Some msg
    else None

