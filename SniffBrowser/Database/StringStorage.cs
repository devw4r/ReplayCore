// For future reference, using LiteDB to store strings.

//using System;
//using System.IO;
//using System.Collections.Generic;

//using LiteDB;
//using SniffBrowser.Core;
//using System.Threading.Tasks;

//namespace SniffBrowser.Database
//{
//    public static class StringStorage
//    {
//        private static readonly string DBPath = $@"{Path.GetTempPath()}\SniffBrowser.db";
//        private static LiteDatabase LiteDB;
//        private static LiteCollection<BsonDocument> Collection;
//        private static readonly Dictionary<uint, string> PendingStringSave = new Dictionary<uint, string>();
//        private static readonly object WriteLock = new object();
//        private static readonly int BulkInsertLimit = 100000; // Max bulk limit.
//        private static double ElapsedSeconds = 0;
//        private static readonly Dictionary<uint, string> Strings = new Dictionary<uint, string>();

//        /// <summary>
//        /// Enable this to use disk instead of memory for LongDescription strings.
//        /// </summary>
//        public static bool UseLocalDB = false;

//        static StringStorage()
//        {
//            Flush();
//        }

//        public static void SaveString(uint id, string text, bool force = false)
//        {
//            // Don't care about empty strings.
//            if (string.IsNullOrEmpty(text))
//                return;

//            if (!UseLocalDB)
//            {
//                Strings?.Add(id, text);
//                return;
//            }

//            try
//            {
//                if (force)
//                {
//                    Collection?.Insert(ToBsonDocument(id, text));
//                }
//                else
//                {
//                    lock (WriteLock)
//                    {
//                        PendingStringSave.Add(id, text);
//                    }
//                }

//                CheckSaving();
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e.ToString());
//            }
//        }

//        private static BsonDocument ToBsonDocument(uint id, string text)
//        {
//            return new BsonDocument
//                        {
//                            { "_id", id.ToString() },
//                            { "t", StringCompressor.CompressString(text) }
//                        };
//        }

//        public static string GetString(uint id)
//        {
//            if (!UseLocalDB)
//            {
//                if (Strings.TryGetValue(id, out string str))
//                    return str;
//            }
//            else
//            {
//                try
//                {
//                    var idStr = id.ToString();
//                    var result = Collection?.FindById(idStr);
//                    if (result == null)
//                        return string.Empty;

//                    if (result.TryGetValue("t", out var val))
//                        return val.AsString;
//                }
//                catch (Exception e)
//                {
//                    Console.WriteLine(e.ToString());
//                }
//            }

//            return string.Empty;
//        }

//        public static void CheckSaving(bool force = false)
//        {
//            if (!UseLocalDB)
//                return;

//            if (PendingStringSave.Count < BulkInsertLimit && !force)
//                return;

//            lock (WriteLock)
//            {
//                Task t = new Task(() =>
//                {
//                    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
//                    sw.Restart();

//                    Console.WriteLine($"Saving strings to local db.");
//                    List<BsonDocument> bulkInsert = new List<BsonDocument>();
//                    foreach (var entry in PendingStringSave)
//                        bulkInsert.Add(ToBsonDocument(entry.Key, entry.Value));
//                    Collection?.InsertBulk(bulkInsert, batchSize: BulkInsertLimit);
//                    bulkInsert.Clear();

//                    PendingStringSave.Clear();
//                    sw.Stop();
//                    Console.WriteLine(sw.Elapsed.TotalSeconds);
//                    ElapsedSeconds += sw.Elapsed.TotalSeconds;
//                    Console.WriteLine(ElapsedSeconds);
//                });

//                t.Start();
//                t.Wait();
//                GC.Collect(); // Need to be pushy on GC.
//            }
//        }

//        public static void Flush()
//        {
//            try
//            {
//                Strings?.Clear();

//                if (UseLocalDB)
//                {
//                    LiteDB?.Dispose();

//                    if (File.Exists(DBPath))
//                    {
//                        File.Delete(DBPath);
//                        Console.WriteLine("Flush local database.");
//                    }

//                    LiteDB = new LiteDatabase(DBPath);
//                    Collection = LiteDB?.GetCollection("strings");
//                }
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e.ToString());
//            }
//        }
//    }
//}
