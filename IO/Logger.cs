using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;


namespace VBoxCleaner.IO
{
    /// <summary>
    /// Writes messages to log file and to debug console
    /// </summary>
    public static class Logger
    {
        public const bool SwitchOn = true;  // Logger becomes dummy when false

        private static readonly FileHolder fHolder;
        private static readonly BlockingCollection<string> LogQueue;
        private static readonly Thread WritingThread;
        private static bool Disposed;
        private static string Header => $"[{DateTime.Now:G}]";

        /// <summary>Writes a string to a log file using DateTime prefix</summary>
        public static void WriteLine(string message)
        {
            if (SwitchOn)
                if (!Disposed && !LogQueue.IsCompleted)
                    try
                    {
                        LogQueue.Add($"{Header} {message}\n");
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Logger WriteLine exception: {e}");
                    }
        }

#pragma warning disable CS8618 
        static Logger()
#pragma warning restore CS8618 
        {
            if (SwitchOn)
            {
                try
                {
                    Disposed = false;
                    fHolder = new();
                    LogQueue = new BlockingCollection<string>(100);

                    WritingThread = new Thread(fHolder.InfiniteWriter)
                    {
                        Name = "Infinite Log Writer",
                        IsBackground = true,
                        Priority = ThreadPriority.BelowNormal
                    };
                    WritingThread.Start();
                }
                catch (Exception ex)
                {
                    Dispose();
                    Debug.WriteLine($"Logger initialization was failed:\n{ex}");
                }
            }
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public static void Dispose()
        {
            if (SwitchOn)
            {
                Disposed = true;
                try
                {
                    LogQueue.CompleteAdding();
                }
                catch (ObjectDisposedException e) { Debug.WriteLine($"LogQueue.CompleteAdding exception:\n{e}"); }
                try
                {
                    WritingThread.Join();  // wait until it ends
                }
                catch (Exception e) { Debug.WriteLine($"Logger.Dispose - WritingThread.Join exception:\n{e}"); }
                fHolder.Dispose();
            }
        }

        internal class FileHolder : IDisposable
        {
            private FileStream _stream = null;

            /// <summary>Returns the existing FileStream or creates a new one</summary>
            /// <exception cref="IOException">
            /// It is meant to be impossible
            /// </exception>
            public FileStream Stream
            {
                get
                {
                    _stream ??= CreateLogFileStream();
                    return _stream;
                }
            }

            /// <summary>Creates a log file and returns its FileStream</summary>
            /// <exception cref="IOException">
            /// It is meant to be impossible
            /// </exception>
            private static FileStream CreateLogFileStream()
            {
                static string TempName(string prefix)
                {
                    Int32 unixTimestamp = (Int32)(DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    return $"{prefix}{unixTimestamp:X}.txt";
                }

                Int32 unixTimestamp = (Int32)(DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                Random rnd = new(unixTimestamp % 65535);

                string startsWith = "log-";
                for (int i = 0; i < 10; i++)  // tries 10 times to create a log file using different names
                {
                    try
                    {
                        FileStream stream = new(TempName(startsWith), FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
                        return stream;
                    }
                    catch (IOException e) { Debug.WriteLine($"CreateLogFileStream IO error: {e}"); }
                    catch (Exception e) { Debug.WriteLine($"CreateLogFileStream error: {e}"); }
                    Thread.Sleep(1);
                    char a = (char)rnd.Next('a', 'z');
                    char b = (char)rnd.Next('a', 'z');
                    char c = (char)rnd.Next('a', 'z');
                    startsWith = $"{startsWith}{a}{b}{c}-";
                }
                throw new IOException("Can not create a log file");
            }

            private void Write(string message)
            {
                Debug.Write(message);
                byte[] encodedText = Encoding.Unicode.GetBytes(message);
                try
                {
                    Stream.Write(encodedText, 0, encodedText.Length);
                    Stream.Seek(0, SeekOrigin.End);
                }
                catch { Debug.Write("FileHolder.CreateLogFileStream: Couldn't write a message to log file"); }
            }

            public void InfiniteWriter()
            {
                Write($"{Header} InfiniteWriter started in Thread: {Thread.CurrentThread.Name}\n");
                try
                {
                    while (true)
                        Write(LogQueue.Take());
                }
                catch (Exception)
                {
                    Write($"{Header} Logger is stopped");
                }
                finally
                {
                    Dispose();
                }
            }

            public void Dispose()
            {
                _stream?.Dispose();
                _stream = null;
            }
        }
    }
}
