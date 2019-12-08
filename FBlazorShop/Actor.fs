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
open Newtonsoft.Json.Linq
open Akkling.Streams
open Newtonsoft.Json
open Akka.Actor
open Akka.Serialization
open System
open System.IO
open System.Text


type PlainNewtonsoftJsonSerializer ( system : ExtendedActorSystem) =
    inherit Serializer(system)
    let settings =
        new JsonSerializerSettings(TypeNameHandling = TypeNameHandling.All,
            MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead)
    let ser = new JsonSerializer()

    override __.IncludeManifest = true


    override __.Identifier = 1711234423;

    override __.ToBinary(o:obj) =
        ser.TypeNameHandling <- TypeNameHandling.All
        ser.MetadataPropertyHandling <-MetadataPropertyHandling.ReadAhead
        let memoryStream = new MemoryStream();
        use streamWriter = new StreamWriter(memoryStream, Encoding.UTF8)
        ser.Serialize(streamWriter, o, o.GetType())
        streamWriter.Flush()
        memoryStream.ToArray()

    override __.FromBinary(bytes : byte array, ttype : Type) =
        ser.TypeNameHandling <- TypeNameHandling.All
        ser.MetadataPropertyHandling <-MetadataPropertyHandling.ReadAhead
        use streamReader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8)
        ser.Deserialize(streamReader, ttype)

let configWithPort port =
    let config = Configuration.parse ("""
        akka {
            actor {
              provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
              serializers {
              json = "Akka.Serialization.NewtonSoftJsonSerializer"
              plainnewtonsoft = "Actor+PlainNewtonsoftJsonSerializer, FBlazorShop"
    }
    serialization-bindings {
              "System.Object" = json
              "Actor+Message, FBlazorShop" = plainnewtonsoft
    }
              #serializers {
               # hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
              #}
              #serialization-bindings {
               // "System.Object" = hyperion
              #}
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

type Tagger  =
    interface IWriteEventAdapter with
        member _.Manifest _ = ""
        member _.ToJournal evt =
                box <| Tagged(evt, deft)
    new () = {}


let system = System.create "cluster-system" (configWithPort 0)
Akka.Cluster.Cluster.Get(system).SelfAddress
    |> Akka.Cluster.Cluster.Get(system).Join


SqlitePersistence.Get(system) |> ignore

let readJournal = PersistenceQuery.Get(system).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

let source = readJournal.EventsByTag("default")
let mat = ActorMaterializer.Create(system);


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

open FSharp.Data.Sql

#if DEBUG

[<Literal>]
let resolutionPath = __SOURCE_DIRECTORY__ + @"\..\FBlazorShop.Web\bin\Debug\netcoreapp3.1"
#else
[<Literal>]
let resolutionPath = __SOURCE_DIRECTORY__ + @"\..\FBlazorShop.Web\bin\Release\netcoreapp3.1"
#endif


[<Literal>]
let connectionString =
    @"Data Source=" + __SOURCE_DIRECTORY__ + @"\..\FBlazorShop.Web\pizza.db;"
type Sql =
    SqlDataProvider<
            Common.DatabaseProviderTypes.SQLITE,
            SQLiteLibrary = Common.SQLiteLibrary.SystemDataSQLite,
            ConnectionString = connectionString,
            ResolutionPath = resolutionPath,
            CaseSensitivityChange = Common.CaseSensitivityChange.ORIGINAL>


let orderFactory () str =

    (AkklingHelpers.entityFactoryFor system "Order"
        <| propsPersist actorProp
        <| None).RefFor AkklingHelpers.DEFAULT_SHARD str



let handleEvent (envelop : EventEnvelope) =
    let ctx = Sql.GetDataContext()
    ctx.Main.Specials|> Seq.iter(fun x -> System.Console.WriteLine(x))
    ()
let init () =
    System.Threading.Thread.Sleep(100)
    let ctx = Sql.GetDataContext()
    ctx.Main.Specials|> Seq.iter(fun x -> System.Console.WriteLine(x))
    source
    |> Source.runForEach mat handleEvent
    |> Async.StartAsTask
    |> ignore
    mat, system


