using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using VBoxCleaner.IO;

namespace VBoxCleaner.Utils
{
    public static class ProcessExtensions
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Проверка совместимости платформы", Justification = "<Ожидание>")]
        public static string GetCommandLine(this Process process)
        {
            try
            {
                using ManagementObjectSearcher searcher = new("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
                using ManagementObjectCollection objects = searcher.Get();
                return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString() ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static string? LogPath(this string? commandLine)
        {
            if (commandLine.IsEmpty()) return null;
            string cmdLine = commandLine!;

            int paramIndex = cmdLine.IndexOf("--sup-hardening-log=");
            if (paramIndex < 0) return null;

            // if path contains whitespaces than the whole param is embraced by quotes
            // "--sup-hardening-log=C:\Path with spaces\Logs\filename.log"
            //  ^                   ^                                    ^
            // paramIndex        pathStart                            pathEnd = "next char after the path" pointer
            int pathStart = cmdLine.IndexOf('=', paramIndex) + 1;
            int pathEnd;
            if (paramIndex > 0 && cmdLine[paramIndex - 1] == '"')
                pathEnd = cmdLine.IndexOf('"', paramIndex);
            else
            {
                pathEnd = cmdLine.IndexOf(' ', paramIndex);
                if (pathEnd < 0) pathEnd = cmdLine.Length;
            }

            if (pathStart < 1 || pathEnd < 0 || pathStart > pathEnd) return null;

            // string fullName = cmdLine[pathStart..pathEnd];
            string fullName = cmdLine.Substring(pathStart, pathEnd - pathStart);
            try
            {
                string? pathName = Path.GetDirectoryName(fullName);
                return pathName.IsEmpty() ? null : pathName;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"LogPath exception: {ex}\nInput command line = ''{cmdLine}''");
                return null;
            }
        }


    }

    public static class StringExtentions
    {
        public static bool IsEmpty(this string? line) => string.IsNullOrEmpty(line);
        public static bool IsNotEmpty(this string? line) => !string.IsNullOrEmpty(line);
        public static string AsHiddenFileName(this string fileName) => new string(fileName
                    .ToCharArray()
                    .Select((@char, index) =>
                    {
                        return (index > 0 && !"_-.".Contains(@char)) ? '*' : @char;
                    })
                    .ToArray());
        public static bool IsVboxLog(this string fullName)
        {
            var file = Path.GetFileName(fullName);
            if (file.StartsWith("VBox") && file.Contains(".log")) return true;
            return false;
        }
    }

    public static class DictionaryExtensions
    {
        public static bool Insert(this Dictionary<string, HashSet<String>> dict, string key, string value)
        {
            if (key.IsEmpty()) throw new ArgumentException("DictionaryExtensions.Insert: Key must not be empty");
            if (value.IsEmpty()) throw new ArgumentException("DictionaryExtensions.Insert: Value must not be empty");

            if (dict.TryGetValue(key, out HashSet<string>? hashSet))
                return hashSet!.Add(value);
            else
            {
                dict.Add(key, [value]);
                return true;
            }
        }

        public static bool DoesntContain(this Dictionary<string, HashSet<String>> dict, string key, string value)
        {
            if (key.IsEmpty()) throw new ArgumentException("DictionaryExtensions.Insert: Key must not be empty");
            if (value.IsEmpty()) throw new ArgumentException("DictionaryExtensions.Insert: Value must not be empty");

            if (dict.TryGetValue(key, out HashSet<string>? hashSet))
                return !hashSet!.Contains(value);
            return true;
        }

#pragma warning disable CS8714 // Тип не может быть использован как параметр типа в универсальном типе или методе. Допустимость значения NULL для аргумента типа не соответствует ограничению "notnull".
        public static K FindFirstKeyByValue<K, V>(this ConcurrentDictionary<K, V> dictionary, V value, K defaultValue)
        {
            //ArgumentNullException.ThrowIfNull(dictionary);
            if (dictionary is null) throw new ArgumentNullException();
            return dictionary.FirstOrDefault(entry =>
                EqualityComparer<V>.Default.Equals(entry.Value, value)).Key ?? defaultValue;
        }
#pragma warning restore CS8714
    }

    public static class IntExtensions
    {
        public static int Seconds(this int value) => value * 1000;
        public static int Minutes(this int value) => value * 1000 * 60;
    }
}
