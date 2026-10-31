using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

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
                CheckEnhancements(database, results);
                CheckMigration(Path.Combine(directory, "legacy-" + Guid.NewGuid().ToString("N") + ".db"), results);
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

        private static void CheckEnhancements(string database, List<string> results)
        {
            long editedId;
            using (var store = new TaskStore(database))
            {
                store.Add("English C++ 阅读", "学习", 2, "2020-01-01");
                var task = store.List().Single(); editedId = task.Id;
                Require(task.Category == "学习" && task.Priority == 2 && task.DueDate == "2020-01-01" && task.IsOverdue, "保存分类、优先级和截止日期，识别逾期", results);
                store.Add("散步", "生活", 0, "");
                store.Add("买菜", "生活", 1, DateTime.Today.ToString("yyyy-MM-dd"));
                Require(store.List()[0].Id == editedId && store.List().Last().Title == "散步", "同状态任务按优先级排序", results);
                Require(!store.List().Single(t => t.Title == "买菜").IsOverdue, "今天截止的任务不算逾期", results);
                Require(TaskStore.Filter(store.List(), "all", "c++", "学习").Single().Id == editedId, "搜索与分类组合，英文大小写不敏感", results);
                Require(TaskStore.Filter(store.List(), "all", "不存在的标题", "").Count == 0, "无匹配搜索返回空列表", results);
                Require(TaskStore.Filter(store.List(), "overdue", "", "").Count == 1, "逾期筛选", results);
                store.SetDone(editedId, true);
                Require(!store.List().Single(t => t.Id == editedId).IsOverdue && TaskStore.Filter(store.List(), "overdue", "", "").Count == 0, "已完成任务不计入逾期", results);
                string created = task.CreatedAt;
                store.Update(editedId, "  修改后的标题  ", "工作", 0, "2030-12-31");
                task = store.List().Single(t => t.Id == editedId);
                Require(task.Title == "修改后的标题" && task.Done && task.CreatedAt == created && task.Category == "工作" && task.Priority == 0, "编辑保留编号、创建时间与完成状态", results);
                foreach (string invalid in new[] { "2030-02-30", "not-a-date", "2030/01/01" })
                {
                    bool rejected = false;
                    try { store.Update(editedId, "不应保存", "工作", 0, invalid); } catch (ArgumentException) { rejected = true; }
                    Require(rejected && store.List().Single(t => t.Id == editedId).Title == "修改后的标题", "无效日期不改动原任务", results);
                }
                bool blankRejected = false;
                try { store.Update(editedId, "　", "生活", 1, ""); } catch (ArgumentException) { blankRejected = true; }
                Require(blankRejected && store.List().Single(t => t.Id == editedId).Title == "修改后的标题", "编辑不允许空标题", results);
                bool categoryRejected = false, priorityRejected = false;
                try { store.Add("无效分类", "未知", 1, ""); } catch (ArgumentException) { categoryRejected = true; }
                try { store.Add("无效优先级", "生活", 5, ""); } catch (ArgumentException) { priorityRejected = true; }
                Require(categoryRejected && priorityRejected && store.List().Count == 3, "拒绝非法分类与优先级", results);
                store.Update(editedId, "无截止日期", "工作", 2, "");
                Require(store.List().Single(t => t.Id == editedId).DueDate == "", "可清除截止日期", results);
            }
            using (var store = new TaskStore(database))
            {
                var task = store.List().Single(t => t.Id == editedId);
                Require(task.Title == "无截止日期" && task.Category == "工作" && task.Priority == 2 && task.DueDate == "" && task.Done, "重开后编辑内容和新字段全部保留", results);
            }
        }

        private static void CheckMigration(string path, List<string> results)
        {
            IntPtr handle;
            if (sqlite3_open_v2(Encoding.UTF8.GetBytes(path + "\0"), out handle, 6, IntPtr.Zero) != 0) throw new Exception("Cannot create legacy fixture");
            try
            {
                string sql = "CREATE TABLE tasks(id INTEGER PRIMARY KEY AUTOINCREMENT,title TEXT NOT NULL,done INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL); INSERT INTO tasks VALUES(42,'旧版任务',1,'2026-01-01 10:00');";
                if (sqlite3_exec(handle, Encoding.UTF8.GetBytes(sql + "\0"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0) throw new Exception("Cannot seed legacy fixture");
            }
            finally { sqlite3_close(handle); }
            using (var store = new TaskStore(path))
            {
                var old = store.List().Single();
                Require(old.Id == 42 && old.Title == "旧版任务" && old.Done && old.CreatedAt == "2026-01-01 10:00", "旧版数据库升级保留全部原始字段", results);
                Require(old.Category == "生活" && old.Priority == 1 && old.DueDate == "", "旧任务获得安全默认值", results);
                store.Add("升级后新增");
                Require(store.List().Any(t => t.Id > 42), "升级后可继续添加任务", results);
            }
            using (var store = new TaskStore(path)) Require(store.List().Count == 2, "迁移可重复运行且不重复添加列", results);
        }
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr data, IntPtr error);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    }
}
