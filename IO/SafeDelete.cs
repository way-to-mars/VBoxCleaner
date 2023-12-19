/* Inspired by:
 * https://github.com/prolice/safedelete
 * by Christophe Van Beneden
 * */

using System;
using System.IO;

namespace VBoxCleaner.IO
{
    internal class SafeDelete
    {
        // My simplified method: 3 random renamings, 2 overwriting steps
        public static bool SecureDelete(string fileName)
        {
            if (!File.Exists(fileName)) return true;
            try
            {
                if (File.GetAttributes(fileName).HasFlag(FileAttributes.ReadOnly))
                    File.SetAttributes(fileName, FileAttributes.Normal);

                string fileDirectory = Path.GetDirectoryName(fileName)!;
                string tempFilePath = fileName;

                long fileSize = new FileInfo(tempFilePath).Length;

                using (FileStream stream = new(tempFilePath, FileMode.Open, FileAccess.Write))
                {
                    OverwriteWithRandomData(stream, fileSize);
                    stream.Seek(0, SeekOrigin.Begin);

                    OverwriteWithZeroValues(stream, fileSize);
                    stream.Seek(0, SeekOrigin.Begin);
                }

                for (int i = 0; i < 3; i++)  // 3 random renamings
                {
                    string randomFileName = Path.GetRandomFileName();
                    randomFileName = Path.Combine(fileDirectory, randomFileName);
                    File.Move(tempFilePath, randomFileName);
                    tempFilePath = randomFileName;
                }

                File.Delete(tempFilePath);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("'SecureDelete' error: " + ex.Message);
                return false;
            }

            return true;
        }

        // the following code is taken from https://github.com/prolice/safedelete
        // mod: add unique seed to Random initialization
        private static void OverwriteWithRandomData(FileStream stream, long fileSize)
        {
            byte[] buffer = new byte[4096];
            Int32 unixTimestamp = (Int32)DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            Random random = new(unixTimestamp % 65535);
            long remainingSize = fileSize;

            while (remainingSize > 0)
            {
                random.NextBytes(buffer);

                int bytesToWrite = (int)Math.Min(remainingSize, buffer.Length);
                stream.Write(buffer, 0, bytesToWrite);

                remainingSize -= bytesToWrite;
            }
        }

        // the following code is taken from https://github.com/prolice/safedelete
        // mod: 'Array.Clear' is moved out of the loop
        private static void OverwriteWithZeroValues(FileStream stream, long fileSize)
        {
            byte[] buffer = new byte[4096];
            long remainingSize = fileSize;
            Array.Clear(buffer, 0, buffer.Length);

            while (remainingSize > 0)
            {
                int bytesToWrite = (int)Math.Min(remainingSize, buffer.Length);
                stream.Write(buffer, 0, bytesToWrite);
                remainingSize -= bytesToWrite;
            }
        }
    }
}
