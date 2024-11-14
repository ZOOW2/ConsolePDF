using System;
using System.IO;
using System.Text.RegularExpressions;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using MySql.Data.MySqlClient;
using System.Timers;

class Program
{
    private static System.Timers.Timer timer;

    static void Main()
    {
        while (true) 
        {
            // Создание таймера с интервалом 5 минут (300000 миллисекунд)
            timer = new System.Timers.Timer(300000);
            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = true;
            timer.Enabled = true;

            // Запуск обработки файлов сразу при запуске программы
            ProcessFiles();

            // Ожидание ввода пользователя для завершения программы
            Console.WriteLine("Write the output if you want to Finish the work.");

            string exit = Console.ReadLine();
            if (exit == "Finish") { Environment.Exit(0); }
            else { continue; }
        }
    }

    private static void OnTimedEvent(Object source, ElapsedEventArgs e)
    {
        ProcessFiles();
    }

    private static void ProcessFiles()
    {
        string folderPath = @"C:\Users\vladi\Desktop\pdf";
        string newFolderPath = @"C:\Users\vladi\Desktop\pdf\processed";

        if (!Directory.Exists(newFolderPath))
        {
            Directory.CreateDirectory(newFolderPath);
        }

        string[] pdfFiles = Directory.GetFiles(folderPath, "*.pdf");

        string connectionString = "server=localhost;user=root;password=root;database=pdf";

        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();

            foreach (string pdfFile in pdfFiles)
            {
                Console.WriteLine($"Считывание файла: {pdfFile}");

                // Извлечение текста из PDF файла
                string text = ExtractTextFromPdf(pdfFile);
                Console.WriteLine($"Содержимое файла: {text}");

                // Поиск номера в содержимом файла
                string number = ExtractNumber(text);

                if (number != null)
                {
                    // Проверяем, есть ли уже такой номер в базе данных
                    if (IsNumberExistInDatabase(conn, number))
                    {
                        Console.WriteLine($"Номер {number} уже существует в базе данных. Файл {pdfFile} игнорируется.");
                        continue;
                    }

                    string newFileName = number + System.IO.Path.GetExtension(pdfFile);
                    string newFilePath = System.IO.Path.Combine(newFolderPath, newFileName);

                    // Копирование файла в новую папку с новым именем
                    File.Copy(pdfFile, newFilePath, true);

                    // Запись данных в базу данных
                    string query = "INSERT INTO info (ID, Name, Path) VALUES (@ID, @Name, @Path)";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", number);
                        cmd.Parameters.AddWithValue("@Name", newFileName);
                        cmd.Parameters.AddWithValue("@Path", newFilePath);

                        cmd.ExecuteNonQuery();
                    }

                    Console.WriteLine($"Файл {pdfFile} скопирован в {newFilePath}. Данные записаны в БД.");
                }
                else
                {
                    Console.WriteLine($"Номер не найден в файле {pdfFile}.");
                }
            }

            conn.Close();
        }
    }

    private static bool IsNumberExistInDatabase(MySqlConnection conn, string number)
    {
        string query = "SELECT COUNT(*) FROM info WHERE ID = @ID";

        using (MySqlCommand cmd = new MySqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@ID", number);
            long count = (long)cmd.ExecuteScalar();
            return count > 0; // Если номер найден, возвращаем true
        }
    }

    private static string ExtractTextFromPdf(string path)
    {
        using (PdfReader reader = new PdfReader(path))
        {
            StringWriter output = new StringWriter();
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                output.WriteLine(PdfTextExtractor.GetTextFromPage(reader, i));
            }
            return output.ToString();
        }
    }

    private static string ExtractNumber(string text)
    {
        // Регулярное выражение для поиска номера в тексте
        Regex regex = new Regex(@"№\s*(\d+)");
        Match match = regex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }
}
