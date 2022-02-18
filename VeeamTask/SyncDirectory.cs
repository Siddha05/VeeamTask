using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace VeeamTask
{
    /// <summary>
    /// Класс односторонней синхронизации двух папок.
    /// <para> 
    /// <list type="bullet">
    /// <listheader>
    /// <term>Особенности и ограничения</term>
    /// </listheader>
    /// <item>
    ///     При нештатной смене системного времени назад и копировании в папку-источник синхронизация не гарантирована
    ///     <para>(решение - использовать события файловой системы)</para>
    /// </item>
    /// <item>
    ///     Атрибуты файлов и папок не синхронизируются 
    /// </item>
    /// <item>
    ///     При ручном изменении папки-реплики она восстанавливается
    ///     <para>(приводится в соответствие папке-источнику)</para>
    /// </item>
    /// <item>
    ///     При копировании в папку-источник во время синхронизации последняя может быть неполной 
    ///     <para>(неучтенные изменения отразятся при следующей синхронизации)</para>
    /// </item>
    /// <item>
    ///     Ошибки во время синхронизации запускают ее с момента последней удачной синхронизации 
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    public class SyncDirectory
    {
        #region Fields
        private DirectoryInfo _s_directory;
        private DirectoryInfo _d_directory;
        private int _period;
        private Timer _timer;
        private DateTime _last_snap_date = DateTime.MinValue;
        private DateTime _preview_snap_date = DateTime.MinValue;
        private DateTime _last_end_sync = DateTime.MinValue;
        private ILogger _logger;
        private List<string> copied_files = new List<string>();
        #endregion

        #region Properties
        /// <summary>
        /// Путь к папке источнику
        /// </summary>
        public string SourceDirectory => _s_directory.FullName;
        /// <summary>
        /// Путь к папке-реплике
        /// </summary>
        public string DestinationDirectory => _d_directory.FullName;
        /// <summary>
        /// Период синхронизации в секундах
        /// <para>Период исчисляется с момента окончания последней синхронизации</para>
        /// </summary>
        public int SyncPeriod => _period;
        /// <summary>
        /// Дата окончания последней синхронизации
        /// </summary>
        public DateTime LastSyncDate => _last_end_sync;
        /// <summary>
        /// Файлы, которые были добавлены при последней неудачной синхронизации
        /// <para>Для избежания повторного копирования в случае неудачной синхронизации. Потенциально может уменьшить кол-во медленных дисковых операций</para>
        /// </summary>
        public IReadOnlyList<string> CopiedFilesAtLastSync => copied_files;
        #endregion

        #region Functions
        private void OnTimerTick(object state)
        {
            Console.WriteLine($"\n================================================ синхронизация начата\n");
            if (!Syncing())
            {
                _logger.Log("Ошибка при синхронизации.");
            }
            else 
            {
                copied_files.Clear();
            }
            Console.WriteLine($"\n================================================ синхронизация окончена");
            _timer.Change(_period * 1000L, Timeout.Infinite);

        }
        public bool ClearDestinationFolder()
        {
            _logger.Log("Очистка папки-реплики");
            try
            {
                foreach (var folder in _d_directory.GetDirectories())
                {
                    DeleteDirectory(folder);
                }
                foreach (var file in _d_directory.GetFiles())
                {
                    DeleteFile(file);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Очистка завершена c ошибками: {ex.Message}");
                return false;
            }
            _logger.Log("Очистка завершена");
            return true;
        }
        public string ToSourceFilePath(FileInfo fileInfo)
                        => Path.Combine(_s_directory.FullName, Path.GetRelativePath(_d_directory.FullName, fileInfo.FullName));
        public string ToDestinationFilePath(FileInfo fileInfo)
                        => Path.Combine(_d_directory.FullName, Path.GetRelativePath(_s_directory.FullName, fileInfo.FullName));
        public string ToSourceDirectoryPath(DirectoryInfo di)
                        => Path.Combine(_s_directory.FullName, Path.GetRelativePath(_d_directory.FullName, di.FullName));
        public string ToDestinationDirectoryPath(DirectoryInfo di)
                        => Path.Combine(_d_directory.FullName, Path.GetRelativePath(_s_directory.FullName, di.FullName));
        public IEnumerable<DirectoryInfo> GetChangedDirectories(DirectoryInfo di, DateTime from)
        {
            return di.GetDirectories("*", SearchOption.AllDirectories)
                                            .Where(n => n.CreationTime > from || n.LastWriteTime > from);
        }
        public IEnumerable<FileInfo> GetChangedFiles(DirectoryInfo di, DateTime from)
        {
            return di.GetFiles("*", SearchOption.AllDirectories)
                                        .Where(n => n.CreationTime > from || n.LastWriteTime > from);
        }
        /// <summary>
        /// Синхронизация папок
        /// </summary>
        /// <returns>True, если синхронизация успешна, иначе - false</returns>
        private bool Syncing()
        {
            try //
            {
                if (HasChangesInDestination()) //для простоты используем полную копию
                {
                    _logger.Log("Папка-реплика была изменена с момента последней синхронизации. Восстанавливаем");
                    RequestForFullCopy();
                }
                ClearMissing(_d_directory);
                _last_snap_date = DateTime.Now;
                foreach (var fi in GetChangedFiles(_s_directory, _preview_snap_date))
                {
                    if (HasFileInDestination(fi))
                    {
                        if (fi.LastAccessTime > _preview_snap_date) UpdateFile(fi);
                    }
                    else
                    {
                        AddFile(fi);
                    }
                }
                CopyDirectoryStructure();
                _last_end_sync = DateTime.Now;
                _preview_snap_date = _last_snap_date;
            }
            catch (Exception ex)
            {
                _logger.Log($"{ex.Message}");
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// Копирует все изменения в структуре каталогов папки-источника (отслеживаем пустые папки)
        /// </summary>
        private void CopyDirectoryStructure()
        {
            foreach (var di in GetChangedDirectories(_s_directory, _preview_snap_date))
            {
                if (!HasDirInDestination(di))
                {
                    AddDirectory(di);
                }
            }
        }
        /// <summary>
        /// Полностью копируем папку-источник при следующей синхронизации
        /// </summary>
        public void RequestForFullCopy() => _preview_snap_date = DateTime.MinValue;
        /// <summary>
        /// Изменялась ли папка-реплика между смежными синхронизациями?
        /// </summary>
        /// <returns></returns>
        private bool HasChangesInDestination()
        {

            if (GetChangedDirectories(_d_directory, _last_end_sync).Any())
            {
                return true;
            }
            if (GetChangedFiles(_d_directory, _last_end_sync).Any())
            {
                return true;
            }
            return false;
        }
        private bool HasFileInSource(FileInfo fi) => File.Exists(ToSourceFilePath(fi));
        private bool HasDirInSource(DirectoryInfo di) => Directory.Exists(ToSourceDirectoryPath(di));
        private bool HasFileInDestination(FileInfo fi) => File.Exists(ToDestinationFilePath(fi));
        private bool HasDirInDestination(DirectoryInfo di) => Directory.Exists(ToDestinationDirectoryPath(di));
        [Obsolete("Only for development mode")]
        public void SetLastSync(DateTime d) => _last_snap_date = d;
        private void UpdateFile(FileInfo fi)
        {
            string dest = ToDestinationFilePath(fi);
            if (copied_files.Contains(dest)) return;
            var old = new FileInfo(dest);
            old.Attributes &= FileAttributes.Normal;
            File.Copy(fi.FullName, dest, true);
            _logger.Log($"Файл обновлен: {dest}");
        }
        private void DeleteFile(FileInfo fileInfo)
        {
            fileInfo.Attributes &= FileAttributes.Normal;
            fileInfo.Delete();
            _logger.Log($"Файл удален: {fileInfo.FullName}");
        }
        private void AddFile(FileInfo fi)
        {
            string dest = ToDestinationFilePath(fi);
            if (!Directory.Exists(ToDestinationDirectoryPath(fi.Directory)))
            {
                AddDirectory(fi.Directory);
            }
            File.Copy(fi.FullName, dest, true);
            copied_files.Add(dest);
            _logger.Log($"Файл добавлен: {dest}");
        }
        private void AddDirectory(DirectoryInfo di)
        {
            string dest = ToDestinationDirectoryPath(di);
            Directory.CreateDirectory(dest);
            _logger.Log($"Папка добавлена: {dest}");
        }
        private void DeleteDirectory(DirectoryInfo di)
        {
            di.Attributes &= FileAttributes.Normal;
            try
            {
                di.Delete(true);
                goto end;
            }
            catch (Exception)
            {
                foreach (var fi in di.GetFiles("*", SearchOption.AllDirectories))
                {
                    DeleteFile(fi);
                }
            }
            di.Delete();
 end:           
            _logger.Log($"Папка и ее содержимое удалены: {di.FullName}");
        }
        private void ClearMissing(DirectoryInfo dirInfo)
        {
            foreach (var fi in dirInfo.GetFiles())
            {
                if (!HasFileInSource(fi))
                {
                    DeleteFile(fi);
                }
            }
            foreach (var di in dirInfo.GetDirectories())
            {
                if (!HasDirInSource(di))
                {
                    DeleteDirectory(di);
                    continue;
                }
                ClearMissing(di);
            }
        }
        /// <summary>
        /// Запуск синхронизации
        /// </summary>
        public void Start()
        {
            _timer.Change(0, Timeout.Infinite);
            _logger.Log("Синхронизация начата");
        }
        /// <summary>
        /// Остановка синхронизации
        /// </summary>
        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _logger.Log("Синхронизация остановлена");
        }
        #endregion
        /// <summary>
        /// Создает объект синхронизации
        /// </summary>
        /// <param name="source">Путь к папке-источнику</param>
        /// <param name="dest">Путь к папке-реплике</param>
        /// <param name="logfile">Путь к Файлу лога</param>
        /// <param name="period">Период синхронизации в сек</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        public SyncDirectory(string source, string dest, string logfile, int period = 100)
        {
            if (!Directory.Exists(source)) throw new ArgumentException("Неверный путь к папке источнику.");
            try
            {
                _logger = new FileLogger(logfile);
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentNullException("Путь к файлу лога не указан");
            }
            catch (DirectoryNotFoundException)
            {
                throw new DirectoryNotFoundException($"Путь к файлу лога '{logfile}' неверный");
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"Путь к файлу лога '{logfile}' неверный");
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Невозможно создать файл лога. {ex.Message}");
            }
            try
            {
                if (!Directory.Exists(dest))
                {
                    _d_directory = Directory.CreateDirectory(dest);
                    _logger.Log("Папка синхронизации создана.");
                }
                else
                {
                    _d_directory = new DirectoryInfo(dest);
                    ClearDestinationFolder();
                }
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentNullException("Путь к папке-реплике не указан");
            }
            catch (DirectoryNotFoundException)
            {
                throw new DirectoryNotFoundException($"Путь к папке-реплике '{dest}' неверный");
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"Путь к папке-реплике '{dest}' неверный");
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Невозможно создать папку-реплику. {ex.Message}");
            }
            if (period < 1) throw new ArgumentException("Период синхронизации не может быть меньше 1 секунды");
            
            _s_directory = new DirectoryInfo(source);
            _period = period;
            _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }
    }
}
