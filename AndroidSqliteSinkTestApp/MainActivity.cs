using Microsoft.Data.Sqlite;
using Serilog;
using SqlKata.Compilers;
using SqlKata.Execution;

namespace AndroidSqliteSinkTestApp
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity
    {
        private string DbPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Logs.db");

        private SqliteConnection GetConnection()
        {
            return new SqliteConnection($"Data Source={DbPath}");
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            Serilog.Debugging.SelfLog.Enable((s =>
            {
                var selfLog = s;
            }));

            if (File.Exists(DbPath))
            {
                //File.Delete(DbPath);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.SQLite(DbPath, retentionPeriod: TimeSpan.FromMinutes(1), batchSize: 3, storeTimestampInUtc: true)
                .CreateLogger();

            Log.Logger.Debug("Hello world");
            Log.Logger.Debug("This is a simple test");
            Log.Logger.Debug("Using sqlite serilog sink");
            Log.Logger.Debug("With the Microsoft.Data.Sqlite package");

            var logs0 = ReadTopXLogsSqlKata(500);

            Task.Run(() =>
            {
                Thread.Sleep(5000);
                var logs1 = ReadLogs();
                var logs2 = ReadLogsSqlKata();
                var logs3 = ReadTopXLogsSqlKata(500);
                Log.Logger.Debug("Bye");
            });
        }

        private List<LogRow> ReadLogs()
        {
            var logs = new List<LogRow>();
            using var connection = GetConnection();
            connection.Open();
            var command = new SqliteCommand("SELECT * FROM Logs", connection);
            var dataReader = command.ExecuteReader();
            while (dataReader.Read())
            {
                var id = dataReader.GetInt16(0);
                var timestamp = dataReader.GetString(1);
                var level = dataReader.GetString(2);
                var exception = dataReader.GetString(3);
                var renderedMessage = dataReader.GetString(4);
                var properties = dataReader.GetString(5);
                logs.Add(new LogRow()
                {
                    id = id,
                    Timestamp = DateTime.Parse(timestamp),
                    Level = level,
                    Exception = exception,
                    RenderedMessage = renderedMessage,
                    Properties = properties,
                });
            }
            return logs;
        }

        private List<LogRow> ReadLogsSqlKata()
        {
            using var connection = GetConnection();
            using var db = new QueryFactory(connection, new SqliteCompiler());

            var logs = db.Query("Logs")
                .Get<LogRow>();

            
            return logs.ToList();
        }

        private List<LogRow> ReadTopXLogsSqlKata(int amount = 1)
        {
            using var connection = GetConnection();
            using var db = new QueryFactory(connection, new SqliteCompiler());

            var last = db.Query("Logs")
                .OrderByDesc("id")
                .FirstOrDefault<LogRow>();

            if (last is null)
                return new List<LogRow>();

            var ToID = last.id;

            var FromID = Math.Max(ToID - amount + 1, 1);

            var logs = db.Query("Logs")
                .Where("id", ">=", FromID)
                .Where("id", "<=", ToID)
                .OrderByDesc("id")
                .Get<LogRow>();


            return logs.ToList();
        }
    }
}