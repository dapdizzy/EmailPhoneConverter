using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EmailPhoneConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string sourceFolder;
                string destinationFolder;

                if (args.Length == 0)
                {
                    sourceFolder = GetCurrentFolderSubfolder(inputFolder);
                    destinationFolder = GetCurrentFolderSubfolder(outputFolder);
                }
                else if (args.Length < 2)
                {
                    Console.WriteLine("Если вы указываете параметры, то необходимо указать два параметра: путь к папке с исходными файлами выгрузки в формате CSV, и путь к выходной папке для формирования выгрузки.");
                    return;
                }
                else
                {
                    sourceFolder = args[0];
                    destinationFolder = args[1];
                }

                if (!Directory.Exists(sourceFolder))
                {
                    Console.WriteLine($"Папка исходных файлов ({sourceFolder}) не существует или нет прав доступа к ней.");
                    return;
                }

                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                Console.WriteLine("Начался процесс обработки");

                foreach (var fileName in Directory.GetFiles(sourceFolder, "*.csv"))
                {
                    var emailsAndPhones = GetEmailAndPhones(fileName);
                    foreach (var platformHashType in PlatformHashType)
                    {
                        var outputFileName = Path.Combine(destinationFolder, GetOutputFileName(platformHashType.Key, Path.GetFileNameWithoutExtension(fileName)));
                        using (var writer = new StreamWriter(File.OpenWrite(outputFileName)))
                        {
                            bool started = false;
                            foreach (var item in emailsAndPhones.Select(value => PreprocessValue(value)))
                            {
                                if (started)
                                {
                                    writer.Write(',');
                                }
                                writer.Write(ComputeHash(platformHashType.Value, item));
                                started = true;
                            }
                        }
                        WriteLineInColor($"Создан файл {outputFileName}", ConsoleColor.Cyan);
                    }
                    WriteLineInColor($"Файл {fileName} обработан.", ConsoleColor.Green);
                }
                using (File.Create(Path.Combine(outputFolder, "Готово.done"))) { } // Dispose the file handler immediately
                Console.WriteLine();
                Console.WriteLine("Готово! ");
                var cts = new CancellationTokenSource(3000);
                Task.WaitAny(
                    Task.Factory.StartNew(async () =>
                    {
                        while (true)
                        {
                            Console.Write(".");
                            await Task.Delay(500);
                        }
                    }, cts.Token).Unwrap(),
                    Task.Factory.StartNew(() => Console.ReadLine(), cts.Token),
                    Task.Delay(3000));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Произошла ошибка:");
                WriteLineInColor(ex.ToString(), ConsoleColor.Red);
                Console.WriteLine("Нажмите <Enter> для завершения.");
                Console.ReadLine();
            }
        }

        private static string GetCurrentFolderSubfolder(string subfolderName)
        {
            return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), subfolderName);
        }

        private static IEnumerable<string> GetEmailAndPhones(string fileName)
        {
            return File.ReadAllLines(fileName)
                .SelectMany(a => a.Split('\n')).Skip(1) // Skip header line
                .SelectMany(
                    line =>
                    {
                        var array = line.Split(';');
                        return new string[] { array[1], array[2] };
                    }
                );
        }

        private static IDictionary<string, HashType> PlatformHashType =>
            new Dictionary<string, HashType>
            {
                ["Google"] = HashType.Sha256,
                ["Yandex"] = HashType.Md5,
                //["VK"] = HashType.Md5,
                //["FB"] = HashType.Sha256
            };

        private static string ComputeHash(HashType hashType, string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var hasher = GetHasher(hashType);
            var inputBytes = Encoding.UTF8.GetBytes(input);

            var hashBytes = hasher.ComputeHash(inputBytes);
            var hash = new StringBuilder();
            foreach (var b in hashBytes)
            {
                hash.Append(string.Format("{0:X2}", b)); // {0:x2}
            }

            return hash.ToString();
        }

        private static IDictionary<HashType, HashAlgorithm> hasherCache = new Dictionary<HashType, HashAlgorithm>();

        private static HashAlgorithm GetHasher(HashType hashType)
        {
            HashAlgorithm hasher;

            if (hasherCache.TryGetValue(hashType, out hasher))
            {
                return hasher;
            }

            switch (hashType)
            {
                case HashType.Md5:
                    hasher = new MD5CryptoServiceProvider();
                    break;
                case HashType.Sha1:
                    hasher = new SHA1Managed();
                    break;
                case HashType.Sha256:
                    hasher = new SHA256Managed();
                    break;
                case HashType.Sha384:
                    hasher = new SHA384Managed();
                    break;
                case HashType.Sha512:
                    hasher = new SHA512Managed();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hashType));
            }

            hasherCache[hashType] = hasher;

            return hasher;
        }

        private static string GetOutputFileName(string platform, string segmentName)
        {
            return $"{segmentName}_{platform}.csv";
        }

        private static Lazy<Regex> emailRegexLazy = new Lazy<Regex>(() => new Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$"));

        private static Regex EmailRegex => emailRegexLazy.Value;

        private static Regex digitRegex = new Regex(@"\d+");

        private static bool IsEmail(string value)
        {
            return EmailRegex.IsMatch(value);
        }

        private static string PreprocessValue(string value)
        {
            if (IsEmail(value))
            {
                return value.Trim().ToLowerInvariant();
            }

            var sb = new StringBuilder();

            var matches = digitRegex.Matches(value);
            for (int i = 0; i < matches.Count; ++i)
            {
                var match = matches[i];
                sb.Append(match.Value);
            }

            return sb.ToString();
        }

        private static void WriteLineInColor(string text, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = oldColor;
        }

        private const string inputFolder = "input";
        private const string outputFolder = "output";
    }
}
