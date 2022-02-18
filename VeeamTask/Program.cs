using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Threading;

namespace VeeamTask
{
    internal class Program
    {
        /// <summary>
        /// По соглашению первый параметр коммандной стороки - папка-источник, второй - папка-реплика
        /// , третий - файл лога, четвертый - период синхронизации в сek
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Неверное количество аргументов командной строки. Должно быть 4");
            }
            else
            {
                SyncDirectory sd = null;
                if (!int.TryParse(args[3], out int period))
                {
                    Console.WriteLine($"Период синхронизации '{args[3]}' задан неверно.");
                    return;
                }
                try
                {
                    sd = new SyncDirectory(args[0], args[1], args[2], period);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при конфигурировании программы. {ex.Message}");
                    return;
                }
                Console.WriteLine("Запускаем? (y/n)");
                string answ = Console.ReadLine();
                if (answ.Equals("yes", StringComparison.OrdinalIgnoreCase) || answ.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    sd.Start();
                    Console.ReadLine();
                    sd.Stop();
                }
                else
                {
                    Console.WriteLine("Очень жаль. До новых встреч");
                }
            }
        }
    }
}
