using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LifeAssistant
{
    internal static class StoreTests
    {
        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string database = Path.Combine(directory, "test-" + Guid.NewGuid().ToString("N") + ".db");
            var results = new List<string>();
            try
            {
                long id;
                using (var store = new TaskStore(database))
                {
                    Require(store.List().Count == 0, "新数据库为空", results);
                    foreach (string title in new[] { "", "   ", "\t\r\n", "　", new string('长', 121) })
                    {
                        bool rejected = false;
                        try { store.Add(title); } catch (ArgumentException) { rejected = true; }
                        Require(rejected && store.List().Count == 0, "拒绝空白或超长标题", results);
                    }
                    store.Add("  阅读 C++：指针与引用 📚  ");
                    var task = store.List().Single(); id = task.Id;
                    Require(task.Title == "阅读 C++：指针与引用 📚" && !task.Done, "添加、去除首尾空白、中文和 emoji", results);
                    store.SetDone(id, true);
                    Require(store.List().Single().Done, "标记完成", results);
                }
                using (var store = new TaskStore(database))
                {
                    Require(store.List().Single().Done, "重新打开数据库后完成状态保留", results);
                    store.SetDone(id, false);
                    Require(!store.List().Single().Done, "恢复未完成", results);
                    store.Add("'; DROP TABLE tasks; --");
                    Require(store.List().Count == 2, "引号及 SQL 字符作为普通标题保存", results);
                    store.SetDone(id, true);
                    Require(!store.List()[0].Done && store.List()[1].Done, "未完成任务排在前面", results);
                    store.Delete(id);
                    Require(store.List().Count == 1, "删除指定任务", results);
                }
                using (var store = new TaskStore(database))
                {
                    Require(store.List().Count == 1 && store.List()[0].Title == "'; DROP TABLE tasks; --", "重开后删除生效且其他任务保留", results);
                    store.Delete(store.List()[0].Id);
                }
                using (var store = new TaskStore(database)) Require(store.List().Count == 0, "删除全部后重开仍为空", results);
                results.Add("ALL PASSED");
                File.WriteAllLines(Path.Combine(directory, "test-results.txt"), results, Encoding.UTF8);
                return 0;
            }
            catch (Exception ex)
            {
                results.Add("FAILED: " + ex);
                File.WriteAllLines(Path.Combine(directory, "test-results.txt"), results, Encoding.UTF8);
                return 1;
            }
        }
        private static void Require(bool condition, string label, List<string> results)
        {
            if (!condition) throw new Exception(label);
            results.Add("PASS: " + label);
        }
    }
}
