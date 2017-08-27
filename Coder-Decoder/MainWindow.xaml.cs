using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
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

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
        
        private void ToImage_Click(object sender, RoutedEventArgs e)
        {
            #region подготовка строки

            string s = textBox.Text.Trim();
            
            FileInfo[] files = new DirectoryInfo(@"symbols").GetFiles();
            string[] alphabet = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                alphabet[i] = files[i].Name.ToLower().Remove(files[i].Name.IndexOf("."));

            Array.Sort(alphabet, new SortByCount());

            s = s.Replace("\r", "");
            while (s.IndexOf("  ") > -1) s = s.Replace("  ", " ");
            foreach (string c in new[] {"\n", ";", "!", "?", ",", ".", ":", "-"})
                s = s.Replace(c, " ");
            s = s.ToLower().Trim().Replace("й", "и").Replace("ё", "е");
            
            if (!CheckString(s, alphabet)) return;

            int Length = s.Length;

            s = s.Replace(" ", "@" + Array.IndexOf(alphabet, "blank") + "@");

            foreach (string c in alphabet)
            {
                if (c!="blank")
                s = s.Replace(c, "@" + Array.IndexOf(alphabet,c) + "@");
            }

            if (s == "") s = "@"+Array.IndexOf(alphabet,"blank")+"@";

            #endregion подготовка строки
            
            #region формирование изображения

            Bitmap answer = new Bitmap(num*40, ((Length - 1)/Num + 1)*40);
            for (int i = 0; i < answer.Width; i++)
                for (int j = 0; j < answer.Height; j++)
                    answer.SetPixel(i, j, Color.White);
            
            int countX = 0, countY = 0;
            foreach (Match m in new Regex(@"[^@]+").Matches(s))
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
                        answer.SetPixel(i + countX*40, j + countY*40, New.GetPixel(i, j));
                
                countX++;
                if (countX == Num)
                {
                    countY++;
                    countX = 0;
                }
            }

            answer.Save("answer.Tiff", ImageFormat.Tiff);
            MessageBox.Show("Ваше сообщение было сохранено в файле answer.tiff", "Готово");

            #endregion формирование изображения
        }

        private bool CheckSymbol(Bitmap input, Bitmap frame)
        {
            for (int i = 0; i < frame.Width; i++)
                for (int j = 0; j < frame.Height; j++)
                    if (input.GetPixel(i, j) != frame.GetPixel(i, j)) return false;
            return true;
        }

        private void ToText_Click(object sender, RoutedEventArgs e)
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

                Bitmap image = LoadBitmap(path);

                int countX = image.Width/40;

                if (image.Width%40 != 0 || image.Height%40 != 0)
                {
                    MessageBox.Show(
                        "Ошибка в размере изображения.\nШирина и высота должна быть кратна 40");
                    return;
                }

                #endregion проверка файла

                #region проверка символов

                int countY = image.Height/40;
                string answer = "";

                for (int p = 0; p < countY; p++)
                    for (int k = 0; k < countX; k++)
                    {

                        Bitmap symbol = new Bitmap(35, 35);
                        Graphics g = Graphics.FromImage(symbol);
                        g.DrawImage(image, 0, 0, new Rectangle(k*40, p*40, 35, 35), GraphicsUnit.Pixel);
                        
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
                            MessageBox.Show("Произошла ошибка в распознавании символа " + (p + k + 1));
                            return;
                        }
                    }
                textBox.Text = answer.Trim();

                MessageBox.Show("Ваше сообщение расшифровано", "Готово");

                #endregion проверка символов
            }
            catch (Exception eh)
            {
                MessageBox.Show(eh+"\n"+eh.Message + "\n\nОбратитесь к разработчику с это ошибкой");
            }
        }
        
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

        private void NumUp_OnClick(object sender, RoutedEventArgs e)
        {
            Num++;
        }
        private void NumDown_OnClick(object sender, RoutedEventArgs e)
        {
            Num--;
        }

        private void CountSymbols_OnKeyUp(object sender, KeyEventArgs e)
        {
            CountSymbols.Text = textBox.Text.Length.ToString();
        }
    }
}
