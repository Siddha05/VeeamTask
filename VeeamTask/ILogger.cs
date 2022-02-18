using System;
using System.IO;

namespace VeeamTask
{
    public interface ILogger
    {
        void Log(string content);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string content) => Console.WriteLine($"{DateTime.Now:G} {content}");
    }
    /// <summary>
    /// Для простоты наследуем от ConsoleLogger. 
    /// </summary>
    public class FileLogger : ConsoleLogger, ILogger
    {
        public string FilePath { get; init; }
        new public async void Log(string content)
        {
            using (var tw = new StreamWriter(FilePath, true))
            {
                await tw.WriteLineAsync($"{DateTime.Now.ToString("G")} {content}");
            }
            base.Log(content);
        }
        /// <summary>
        /// Создает объект логирующий как в консоль, так и в файл
        /// </summary>
        /// <param name="filepath">Путь к файлу лога</param>
        /// <exception cref="Exception">Файл лога не создан</exception>
        public FileLogger(string filepath)
        {
            if (File.Exists(filepath))
            {
                FilePath = filepath;
            }
            else
            {
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                    }
                    using (var fs = File.Create(filepath))
                        fs.Close();
                    FilePath = filepath;
                    Log($"Создан файл лога {filepath}");                   
                }
                catch (Exception)
                {
                    throw new Exception("Не удалось создать файл лога.");
                }
            }
        }
    }
}
