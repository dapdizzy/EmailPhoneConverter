using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailPhoneConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Необходимо указать два параметра: путь к папке с исходными файлами выгрузки в формате CSV, и путь к выходной папке для формирования выгрузки.");
                return;
            }

            var sourceFolder = args[0];
            if (!Directory.Exists(sourceFolder))
            {
                Console.WriteLine($"Папка исходных файлов ({sourceFolder}) не существует или нет прав доступа к ней.");
                return;
            }

            var destinationFolder = args[1];
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

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
                    Console.WriteLine($"Создан файл {outputFileName}");
                }
                Console.WriteLine($"Файл {fileName} обработан.");
            }
            Console.WriteLine("Готово!");
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
                ["VK"] = HashType.Md5,
                ["FB"] = HashType.Sha256
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

        private static HashAlgorithm GetHasher(HashType hashType)
        {
            switch (hashType)
            {
                case HashType.Md5:
                    return new MD5CryptoServiceProvider();
                case HashType.Sha1:
                    return new SHA1Managed();
                case HashType.Sha256:
                    return new SHA256Managed();
                case HashType.Sha384:
                    return new SHA384Managed();
                case HashType.Sha512:
                    return new SHA512Managed();
                default:
                    throw new ArgumentOutOfRangeException(nameof(hashType));
            }
        }

        private static string GetOutputFileName(string platform, string segmentName)
        {
            return $"{segmentName}{platform}.csv";
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
    }
}
