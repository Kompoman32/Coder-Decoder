using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Color = System.Drawing.Color;

namespace Coder_Decoder
{
    class SortByCount : IComparer
    {
        public int Compare(object s1, object s2)
        {
            return ((string) s2).Length.CompareTo(((string) s1).Length);
        }
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            workerToText=new BackgroundWorker();
            workerToImage = new BackgroundWorker();
            workerToImage.DoWork += new DoWorkEventHandler(worker_ToImage);
            workerToText.DoWork += new DoWorkEventHandler(worker_ToText);
            // textBox.Text = "Варя классная";
        }
        
        Bitmap LoadBitmap(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open);
            Bitmap b = new Bitmap(fs,false);
            fs.Close();
            return b;
        }

        bool CheckString(string s, string[] pattern)
        {
            string h = "";
            foreach (string c in pattern)
                if (c != "blank")
                    h += @"(" + c + @")|";
            h = h.Trim('|');

            if (new Regex(@"(" + h + @"| )+").Match(s).Length == s.Length) return true;

            h = "'" + h.Trim('(', ')').Replace(")|(", "', '") +
                "', ';', '!', '?', ',', '.', ':', '-', пробел, перенос строки";
            MessageBox.Show("Обнаружены неизвестные символы.\n Доступные символы:\n" + "\n" + h);
            return false;
        }

        ////////////Text To Image\\\\\\\\\\\\\\\\\\\\\\
        string text;
        int Length;
        string[] alphabet;

        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp, Object value); // Обновление ProgressBara
        
        BackgroundWorker workerToImage;
        void worker_ToImage(object sender, DoWorkEventArgs e) { TextToImage(); }

        private void ToImage_Click(object sender, RoutedEventArgs e)
        {
            #region подготовка строки

            string s = textBox.Text.Trim();

            FileInfo[] files = new DirectoryInfo(@"symbols").GetFiles();
            alphabet = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                alphabet[i] = files[i].Name.ToLower().Remove(files[i].Name.IndexOf("."));

            Array.Sort(alphabet, new SortByCount());

            s = s.Replace("\r", "");
            while (s.IndexOf("  ") > -1) s = s.Replace("  ", " ");
            foreach (string c in new[] { "\n", ";", "!", "?", ",", ".", ":", "-" })
                s = s.Replace(c, " ");
            s = s.ToLower().Trim().Replace("й", "и").Replace("ё", "е");

            if (!CheckString(s, alphabet)) return;

            Length = s.Length;

            s = s.Replace(" ", "@" + Array.IndexOf(alphabet, "blank") + "@");

            foreach (string c in alphabet)
            {
                if (c != "blank")
                    s = s.Replace(c, "@" + Array.IndexOf(alphabet, c) + "@");
            }

            if (s == "") s = "@" + Array.IndexOf(alphabet, "blank") + "@";

            #endregion подготовка строки

            text = s;
            Bar.Minimum = 0;
            Bar.Maximum = Length;
            Bar.Value = 0;
            workerToImage.RunWorkerAsync(); // асинхронная работа с progress bar'ом и создание изображения
        }
        private void TextToImage()
        {
            #region формирование изображения

            Bitmap answer = new Bitmap(num * 40, ((Length - 1) / Num + 1) * 40);
            /*for (int i = 0; i < answer.Width; i++)
                for (int j = 0; j < answer.Height; j++)
                    answer.SetPixel(i, j, Color.White);
            */
            int countX = 0, countY = 0;

            
            UpdateProgressBarDelegate updProgress = new UpdateProgressBarDelegate(Bar.SetValue);
            double value;
            
            foreach (Match m in new Regex(@"[^@]+").Matches(text))
            {
                string path = m.Value.Replace("@", "");
                path = alphabet[Convert.ToInt32(path)];
                //if (path == " ") path = "blank";

                Bitmap New = LoadBitmap("symbols/" + path + ".Tiff");

                if (!(New.Height == 35 && New.Width == 35))
                {
                    MessageBox.Show("Размер петтерна символа " + path + " должен быть 35px на 35px");
                    return;
                }

                for (int i = 0; i < New.Width; i++)
                    for (int j = 0; j < New.Height; j++)
                        answer.SetPixel(i + countX * 40, j + countY * 40, New.GetPixel(i, j));

                countX++;
                if (countX == Num)
                {
                    countY++;
                    countX = 0;
                }

                value = countX + Num * countY;
                Dispatcher.Invoke(updProgress, new object[] { ProgressBar.ValueProperty, value });
            }

            answer.Save("answer.Tiff", ImageFormat.Tiff);
            MessageBox.Show("Ваше сообщение было сохранено в файле answer.tiff", "Готово");

            #endregion формирование изображения

            text = "";
            Length = 0;
            alphabet = null;
        }

        ////////////Image To Text\\\\\\\\\\\\\\\\\\\\\\
        BackgroundWorker workerToText;
        void worker_ToText(object sender, DoWorkEventArgs e) { ImageToText(); }

        private delegate void UpdateTextVoxDelegate(System.Windows.DependencyProperty dp, Object value);    // Обновление TextBoxa
        UpdateTextVoxDelegate ll;   // Делегат для обновления TextBoxa
        
        Bitmap image = new Bitmap(35, 35);
        
        private bool CheckSymbol(Bitmap input, Bitmap frame)// Проверка символа
        {
            for (int i = 0; i < frame.Width; i++)
                for (int j = 0; j < frame.Height; j++)
                    if (input.GetPixel(i, j) != frame.GetPixel(i, j))
                        return false;
            return true;
        }
        
        private void ToText_Click(object sender, RoutedEventArgs e)// Кнопка
        {
            try
            {
                #region Проверка файла

                OpenFileDialog of = new OpenFileDialog();
                of.FileName = "";
                of.Filter = "Файлы рисунков (*.Tiff)|*.Tiff";
                of.CheckFileExists = true;
                of.ValidateNames = true;
                of.Title = "Расшифровать изображение";
                of.CheckPathExists = true;
                of.ValidateNames = true;
                bool? gg = of.ShowDialog(this);

                if (gg == null || gg == false) return;

                string path = of.FileName;

                image = LoadBitmap(path);

                if (image.Width % 40 != 0 || image.Height % 40 != 0)
                {
                    MessageBox.Show(
                        "Ошибка в размере изображения.\nШирина и высота должна быть кратна 40");
                    return;
                }

                #endregion проверка файла

                Bar.Minimum = 0;
                Bar.Maximum = Length;
                Bar.Value = 0;
                ll=new UpdateTextVoxDelegate(textBox.SetValue); // добавление делегата, чтобы внутри асинхронного метода изменить textBox.Text
                workerToText.RunWorkerAsync(); // асинхронная работа с progress bar'ом и создание строки
            }
            catch (Exception eh)
            {
                MessageBox.Show(eh + "\n" + eh.Message + "\n\nОбратитесь к разработчику с это ошибкой");
            }
        }
        private void ImageToText()
        {
            #region проверка символов

            int countY = image.Height / 40;
            int countX = image.Width / 40;
            string answer = "";

            double value;
            UpdateProgressBarDelegate updProgress = new UpdateProgressBarDelegate(Bar.SetValue);

            for (int p = 0; p < countY; p++)
                for (int k = 0; k < countX; k++)
                {

                    Bitmap symbol = new Bitmap(35, 35);
                    Graphics g = Graphics.FromImage(symbol);
                    g.DrawImage(image, 0, 0, new Rectangle(k * 40, p * 40, 35, 35), GraphicsUnit.Pixel);

                    //symbol = image.Clone(new Rectangle(k*40, p*40, 35, 35), PixelFormat.Format32bppArgb);

                    FileInfo[] dd = new DirectoryInfo(@"symbols").GetFiles();
                    bool ok = false;
                    foreach (FileInfo f in dd)
                    {
                        Bitmap frame = LoadBitmap(@"symbols/" + f.Name);
                        if (CheckSymbol(symbol, frame))
                        {
                            ok = true;
                            string s = f.Name.Remove(f.Name.IndexOf("."));
                            answer += s == "blank" ? " " : s;
                            break;
                        }
                    }
                    if (!ok)
                    {
                        MessageBox.Show("Произошла ошибка в распознавании символа " + (p*countY + k + 1));
                        return;
                    }
                    value = p + countX * k;
                    Dispatcher.Invoke(updProgress, ProgressBar.ValueProperty, value);
                }

            MessageBox.Show("Ваше сообщение расшифровано", "Готово");
            image = null;

            Dispatcher.Invoke(ll, TextBox.TextProperty, answer.Trim());
            #endregion проверка символов
        }

        ////////////Кол-во символов в строке\\\\\\\\\\\\\\\\\\\\
        const int NumMin=1;
        int num=NumMin;
        public int Num
        {
            get
            {
                return num;
            }
            set
            {
                num = value < NumMin ? NumMin : value;
                charCount.Text = num.ToString();
            }
        }

        private void NumUp_OnClick(object sender, RoutedEventArgs e)// Стрелка вверх
        {
            Num++;
        }
        private void NumDown_OnClick(object sender, RoutedEventArgs e)// Стрелка вниз
        {
            Num--;
        }
        private void charCount_KeyDown(object sender, KeyEventArgs e)// Ввод с клавиатуры
        {
            switch (e.Key)
            {
                #region доступные клавиши
                case Key.D0:
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                case Key.NumPad0:
                case Key.NumPad1:
                case Key.NumPad2:
                case Key.NumPad3:
                case Key.NumPad4:
                case Key.NumPad5:
                case Key.NumPad6:
                case Key.NumPad7:
                case Key.NumPad8:
                case Key.NumPad9:
                #endregion доступные клавиши
                e.Handled = false;
                break;
                default:
                e.Handled = true;
                break;
            }
        }
        private void charCount_LostFocus(object sender, RoutedEventArgs e) // Изменение кол-ва символов в строке
        {
            if (charCount.Text == "") charCount.Text = "0";

            Num = int.Parse(charCount.Text);
        }

        private void CountSymbols_OnKeyUp(object sender, KeyEventArgs e) // Количество символов в тексте
        {
            CountSymbols.Text = textBox.Text.Length.ToString();
        }
    }
}
