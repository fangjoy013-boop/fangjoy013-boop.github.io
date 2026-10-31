using System;
using System.Globalization;
using System.Collections.Generic;
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
using System.Windows.Input;
using System.Windows.Threading;

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
        private long? editingId;
        private List<TodoTask> tasks = new List<TodoTask>();
        private readonly DispatcherTimer dayTimer = new DispatcherTimer();

        public TodoController(Window window, TaskStore store, string databasePath)
        {
            this.window = window; this.store = store;
            input = Find<TextBox>("TaskInput"); list = Find<ItemsControl>("TaskList");
            Find<ComboBox>("CategoryInput").ItemsSource = TaskStore.Categories;
            Find<ComboBox>("CategoryInput").SelectedIndex = 0;
            Find<ComboBox>("PriorityInput").ItemsSource = new[] { "低优先级", "普通", "高优先级" };
            Find<ComboBox>("PriorityInput").SelectedIndex = 1;
            Find<ComboBox>("CategoryFilter").ItemsSource = new[] { "全部分类" }.Concat(TaskStore.Categories).ToArray();
            Find<ComboBox>("CategoryFilter").SelectedIndex = 0;
            Find<TextBlock>("DateText").Text = DateTime.Now.ToString("yyyy年M月d日  dddd", CultureInfo.GetCultureInfo("zh-CN"));
            Find<TextBlock>("SaveStatus").ToolTip = "数据文件：" + databasePath;
            Find<Button>("AddButton").Click += delegate { Add(); };
            Find<Button>("CancelEditButton").Click += delegate { ResetForm(); Feedback("已取消编辑，原任务未改动。", false); };
            input.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; Add(); } };
            input.TextChanged += delegate { Find<TextBlock>("InputHint").Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            Find<RadioButton>("FilterAll").Checked += delegate { filter = "all"; ApplyView(); };
            Find<RadioButton>("FilterPending").Checked += delegate { filter = "pending"; ApplyView(); };
            Find<RadioButton>("FilterDone").Checked += delegate { filter = "done"; ApplyView(); };
            Find<RadioButton>("FilterOverdue").Checked += delegate { filter = "overdue"; ApplyView(); };
            Find<TextBox>("SearchInput").TextChanged += delegate { Find<TextBlock>("SearchHint").Visibility = Find<TextBox>("SearchInput").Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; ApplyView(); };
            Find<ComboBox>("CategoryFilter").SelectionChanged += delegate { ApplyView(); };
            Find<Button>("ClearFiltersButton").Click += delegate { ResetFilters(); };
            list.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnTaskButton));
            window.Loaded += delegate { input.Focus(); };
            Reload();
            // 跨过午夜后刷新逾期标记，不需要重启。
            dayTimer.Interval = TimeSpan.FromMinutes(1);
            dayTimer.Tick += delegate { Find<TextBlock>("DateText").Text = DateTime.Now.ToString("yyyy年M月d日  dddd", CultureInfo.GetCultureInfo("zh-CN")); ApplyView(); };
            dayTimer.Start();
            window.Closed += delegate { dayTimer.Stop(); };
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (editingId.HasValue && MessageBox.Show(window, "当前编辑尚未保存，确定关闭吗？", "未保存的修改", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) e.Cancel = true;
            };
        }
        private T Find<T>(string name) where T : class { return (T)window.FindName(name); }
        private void Add()
        {
            bool editing = editingId.HasValue;
            if (Change(delegate
            {
                string category = Find<ComboBox>("CategoryInput").SelectedItem as string;
                int priority = Find<ComboBox>("PriorityInput").SelectedIndex;
                string dueDate = Find<TextBox>("DueInput").Text.Trim();
                if (editingId.HasValue) store.Update(editingId.Value, input.Text, category, priority, dueDate);
                else store.Add(input.Text, category, priority, dueDate);
            }, editing ? "修改已保存，任务完成状态保持不变。" : "任务已添加并保存。"))
            {
                ResetForm();
                ResetFilters();
            }
        }
        private void ResetForm()
        {
            editingId = null; input.Clear();
            Find<ComboBox>("CategoryInput").SelectedIndex = 0;
            Find<ComboBox>("PriorityInput").SelectedIndex = 1;
            Find<TextBox>("DueInput").Clear();
            Find<TextBlock>("FormTitle").Text = "添加一件小事";
            Find<Button>("AddButton").Content = "＋ 添加任务";
            Find<Button>("CancelEditButton").Visibility = Visibility.Collapsed;
            input.Focus();
        }
        private void ResetFilters()
        {
            Find<TextBox>("SearchInput").Clear();
            Find<ComboBox>("CategoryFilter").SelectedIndex = 0;
            Find<RadioButton>("FilterAll").IsChecked = true;
            filter = "all"; ApplyView();
        }
        private void BeginEdit(TodoTask task)
        {
            if (editingId.HasValue && MessageBox.Show(window, "放弃当前未保存的编辑，切换到这项任务？", "切换编辑", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            editingId = task.Id; input.Text = task.Title;
            Find<ComboBox>("CategoryInput").SelectedItem = task.Category;
            Find<ComboBox>("PriorityInput").SelectedIndex = task.Priority;
            Find<TextBox>("DueInput").Text = task.DueDate;
            Find<TextBlock>("FormTitle").Text = "编辑任务";
            Find<Button>("AddButton").Content = "保存修改";
            Find<Button>("CancelEditButton").Visibility = Visibility.Visible;
            Feedback("修改上方内容后保存；点击取消可保留原任务。", false);
            input.Focus(); input.SelectAll();
        }
        private void OnTaskButton(object sender, RoutedEventArgs e)
        {
            var button = e.OriginalSource as Button;
            if (button == null) return;
            var task = button.DataContext as TodoTask;
            if (task == null) return;
            e.Handled = true;
            if ((string)button.Tag == "edit") BeginEdit(task);
            else if ((string)button.Tag == "toggle") Change(delegate { store.SetDone(task.Id, !task.Done); }, task.Done ? "任务已恢复为待完成并保存。" : "又完成一件事，已保存。" );
            else if ((string)button.Tag == "delete")
            {
                if (MessageBox.Show(window, "确定删除这项任务吗？\n\n" + task.Title, "删除任务", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    if (Change(delegate { store.Delete(task.Id); }, "任务已删除并保存。") && editingId == task.Id) ResetForm();
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
            tasks = store.List();
            Find<TextBlock>("TotalCount").Text = tasks.Count.ToString();
            Find<TextBlock>("PendingCount").Text = tasks.Count(t => !t.Done).ToString();
            Find<TextBlock>("DoneCount").Text = tasks.Count(t => t.Done).ToString();
            ApplyView();
        }
        private void ApplyView()
        {
            string query = Find<TextBox>("SearchInput").Text;
            var categoryBox = Find<ComboBox>("CategoryFilter");
            string category = categoryBox.SelectedIndex > 0 ? categoryBox.SelectedItem as string : "";
            var visible = TaskStore.Filter(tasks, filter, query, category);
            list.ItemsSource = visible;
            Find<TextBlock>("ResultCount").Text = "显示 " + visible.Count + " / " + tasks.Count + " 项";
            Find<StackPanel>("EmptyState").Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Find<TextBlock>("EmptyTitle").Text = filter == "done" ? "还没有已完成的任务" : filter == "pending" ? "待办已清空，做得不错" : "从第一件小事开始";
            Find<TextBlock>("EmptyHint").Text = filter == "done" ? "完成一件小事，它就会出现在这里。" : filter == "pending" ? "歇一歇，或者为自己安排下一件小事。" : "在上方写下任务，为生活腾出一点条理。";
            if (!String.IsNullOrWhiteSpace(query) || !String.IsNullOrEmpty(category))
            {
                Find<TextBlock>("EmptyTitle").Text = "没有匹配的任务";
                Find<TextBlock>("EmptyHint").Text = "试试其他关键词，或点击“重置筛选”。";
            }
            else if (filter == "overdue")
            {
                Find<TextBlock>("EmptyTitle").Text = "没有逾期任务";
                Find<TextBlock>("EmptyHint").Text = "设置截止日期后，可以在这里关注逾期事项。";
            }
        }
    }
}
