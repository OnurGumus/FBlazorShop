module Actor

open Akka.Persistence.Sqlite
open Akkling
open Akka.Cluster.Tools.Singleton
open Akka.Persistence.Query
open Akka.Persistence.Query.Sql
open Akka.Streams
open Akka.Persistence.Journal
open System.Collections.Immutable
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
                    "Domain+Order+Message, FBlazorShop" = plainnewtonsoft
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
                        "Domain+Order+Message, FBlazorShop" = tagger
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

let mat = ActorMaterializer.Create(system);

open FSharp.Data.Sql
open System.Runtime.InteropServices

[<Literal>]
let resolutionPath = __SOURCE_DIRECTORY__ + @"\..\FBlazorShop.Web\net46"

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

if not System.Environment.Is64BitProcess then
    let path = System.Environment.CurrentDirectory
    NativeLibrary.Load(Path.Combine(path, @"net46\SQLite.Interop.dll")) |>ignore

let ctx = Sql.GetDataContext("Data Source=pizza.db;" )




