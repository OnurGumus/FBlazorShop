module Actor

open Akka.Persistence.Sqlite
open Akkling
open Akka.Cluster.Tools
open Akka.Cluster.Tools.Singleton
open Akkling.Persistence
open FBlazorShop.App.Model
open Akka.Persistence.Query
open Akka.Persistence.Query.Sql
open Akka.Streams
open Akka.Persistence.Journal
open System.Collections.Immutable


let configWithPort port =
    let config = Configuration.parse ("""
        akka {
            actor {
              provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
              serializers {
                hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
              }
              serialization-bindings {
               // "System.Object" = hyperion
              }
            }
          remote {
            helios.tcp {
              public-hostname = "localhost"
              hostname = "localhost"
              port = """ + port.ToString() + """
            }
          }
          cluster {
            auto-down-unreachable-after = 5s
         //   seed-nodes = [ "akka.tcp://cluster-system@localhost:12345" ]
           // sharding.remember-entities = true
          }
          persistence{
            query.journal.sql {
                # Implementation class of the SQL ReadJournalProvider
                class = "Akka.Persistence.Query.Sql.SqlReadJournalProvider, Akka.Persistence.Query.Sql"

                # Absolute path to the write journal plugin configuration entry that this
                # query journal will connect to.
                # If undefined (or "") it will connect to the default journal as specified by the
                # akka.persistence.journal.plugin property.
                write-plugin = ""

                # The SQL write journal is notifying the query side as soon as things
                # are persisted, but for efficiency reasons the query side retrieves the events
                # in batches that sometimes can be delayed up to the configured `refresh-interval`.
                refresh-interval = 1s

                # How many events to fetch in one query (replay) and keep buffered until they
                # are delivered downstreams.
                max-buffer-size = 100
            }
            journal {
              plugin = "akka.persistence.journal.sqlite"
              sqlite
              {
                  connection-string = "Data Source=pizza.db;"
                  auto-initialize = on
                  event-adapters.tagger = "Actor+Tagger, FBlazorShop"
                  event-adapter-bindings {
                     "Actor+Message, FBlazorShop" = tagger
                  }

              }
            }
          snapshot-store{
            plugin = "akka.persistence.snapshot-store.sqlite"
            sqlite {
                auto-initialize = on
                connection-string = "Data Source=pizza.db"
            }
          }
        }
        }
        """)
    config.WithFallback(ClusterSingletonManager.DefaultConfig())

type Command = PlaceOrder of Order
type Event = OrderPlaced of Order

type Message =
    | Command of Command
    | Event of Event

let deft = ImmutableHashSet.Create("default")

type Tagger () =
    interface IWriteEventAdapter with
        member _.Manifest _ = ""
        member _.ToJournal evt =
            match evt with
            | :? Message ->
                box <| Tagged(evt, deft)
            | _ -> evt


let system = System.create "cluster-system" (configWithPort 0)
Akka.Cluster.Cluster.Get(system).SelfAddress
    |> Akka.Cluster.Cluster.Get(system).Join

System.Threading.Thread.Sleep(2000)

SqlitePersistence.Get(system) |> ignore

let readJournal = PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

let source = readJournal.EventsByTag("default")
let mat = ActorMaterializer.Create(system);
System.Threading.Thread.Sleep(2000)
source.RunForeach((fun e ->System.Console.WriteLine(e)), mat) |> ignore

let actorProp (mailbox : Eventsourced<_>)=
  let rec set (state : Order option) =
    actor {
      let! (msg) = mailbox.Receive()
      match msg with
      | Event (OrderPlaced o) when mailbox.IsRecovering () ->
            return! o |> Some |> set
      | Command(PlaceOrder o) ->
        return  o |> OrderPlaced |> Event |> Persist
      | Persisted mailbox (Event(OrderPlaced o)) ->
         return! o |> Some |> set
      | _ -> invalidOp "not supported"
    }
  set None




let orderFactory =
    (AkklingHelpers.entityFactoryFor system "Order"
        <| propsPersist actorProp
        <| None).RefFor AkklingHelpers.DEFAULT_SHARD

let init () =  mat, system


