using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace LifeAssistant
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--self-test") return StoreTests.Run(args[1]);
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            if (args.Length == 2 && args[0] == "--data-dir") directory = Path.GetFullPath(args[1]);
            try
            {
                string token;
                using (var sha = SHA256.Create()) token = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(directory.ToUpperInvariant()))).Replace("-", "");
                bool created;
                using (var mutex = new Mutex(true, "Local\\LifeAssistant_" + token, out created))
                {
                    if (!created) { MessageBox.Show("待办助手已在运行，请切换到已打开的窗口。", "拾光"); return 0; }
                    try
                    {
                        using (var store = new TaskStore(Path.Combine(directory, "tasks.db")))
                        {
                            var app = new Application();
                            Window window;
                            using (var xaml = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) window = (Window)XamlReader.Load(xaml);
                            var controller = new TodoController(window, store, Path.Combine(directory, "tasks.db"));
                            app.Run(window);
                            GC.KeepAlive(controller);
                        }
                    }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法启动个人生活助手。\n\n" + ex.Message + "\n\n请将整个项目放在可写的文件夹内，然后重试。原有数据库不会被自动清空。", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }

    // 控制器：把按钮点击连接到数据库操作，类似 C++ 中的事件处理函数。
    public sealed class TodoController
    {
        private readonly Window window;
        private readonly TaskStore store;
        private readonly TextBox input;
        private readonly ItemsControl list;
        private string filter = "all";

        public TodoController(Window window, TaskStore store, string databasePath)
        {
            this.window = window; this.store = store;
            input = Find<TextBox>("TaskInput"); list = Find<ItemsControl>("TaskList");
            Find<TextBlock>("DateText").Text = DateTime.Now.ToString("yyyy年M月d日  dddd", CultureInfo.GetCultureInfo("zh-CN"));
            Find<TextBlock>("SaveStatus").ToolTip = "数据文件：" + databasePath;
            Find<Button>("AddButton").Click += delegate { Add(); };
            input.TextChanged += delegate { Find<TextBlock>("InputHint").Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            Find<RadioButton>("FilterAll").Checked += delegate { filter = "all"; Reload(); };
            Find<RadioButton>("FilterPending").Checked += delegate { filter = "pending"; Reload(); };
            Find<RadioButton>("FilterDone").Checked += delegate { filter = "done"; Reload(); };
            list.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnTaskButton));
            window.Loaded += delegate { input.Focus(); };
            Reload();
        }
        private T Find<T>(string name) where T : class { return (T)window.FindName(name); }
        private void Add()
        {
            if (Change(delegate { store.Add(input.Text); }, "任务已添加并保存。"))
            {
                input.Clear();
                Find<RadioButton>("FilterAll").IsChecked = true;
                input.Focus();
            }
        }
        private void OnTaskButton(object sender, RoutedEventArgs e)
        {
            var button = e.OriginalSource as Button;
            if (button == null) return;
            var task = button.DataContext as TodoTask;
            if (task == null) return;
            e.Handled = true;
            if ((string)button.Tag == "toggle") Change(delegate { store.SetDone(task.Id, !task.Done); }, task.Done ? "任务已恢复为待完成并保存。" : "又完成一件事，已保存。" );
            else if ((string)button.Tag == "delete")
            {
                if (MessageBox.Show(window, "确定删除这项任务吗？\n\n" + task.Title, "删除任务", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    Change(delegate { store.Delete(task.Id); }, "任务已删除并保存。");
            }
        }
        private bool Change(Action action, string message)
        {
            try
            {
                action(); // SQLite 成功提交后，才更新界面和“已保存”提示。
                Reload();
                Feedback(message, false);
                Find<TextBlock>("SaveStatus").Text = "●  已保存到本机 · " + DateTime.Now.ToString("HH:mm:ss") + "  ·  关闭后仍会保留";
                return true;
            }
            catch (ArgumentException ex) { Feedback(ex.Message, true); input.Focus(); return false; }
            catch (Exception ex)
            {
                Feedback("操作未完成，请重试。" + ex.Message, true);
                Find<TextBlock>("SaveStatus").Text = "！ 保存失败，请检查文件夹权限或剩余磁盘空间。";
                return false;
            }
        }
        private void Feedback(string message, bool error)
        {
            var feedback = Find<TextBlock>("Feedback"); feedback.Text = message;
            feedback.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(error ? "#B14756" : "#377C61"));
        }
        private void Reload()
        {
            var tasks = store.List();
            Find<TextBlock>("TotalCount").Text = tasks.Count.ToString();
            Find<TextBlock>("PendingCount").Text = tasks.Count(t => !t.Done).ToString();
            Find<TextBlock>("DoneCount").Text = tasks.Count(t => t.Done).ToString();
            var visible = tasks.Where(t => filter == "all" || (filter == "done" ? t.Done : !t.Done)).ToList();
            list.ItemsSource = visible;
            Find<StackPanel>("EmptyState").Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Find<TextBlock>("EmptyTitle").Text = filter == "done" ? "还没有已完成的任务" : filter == "pending" ? "待办已清空，做得不错" : "从第一件小事开始";
            Find<TextBlock>("EmptyHint").Text = filter == "done" ? "完成一件小事，它就会出现在这里。" : filter == "pending" ? "歇一歇，或者为自己安排下一件小事。" : "在上方写下任务，为生活腾出一点条理。";
        }
    }
}
