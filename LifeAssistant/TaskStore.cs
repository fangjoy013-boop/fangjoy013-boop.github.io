using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LifeAssistant
{
    // 相当于 C++ 的 struct：保存一条任务的数据。
    public sealed class TodoTask
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public bool Done { get; set; }
        public string CreatedAt { get; set; }
        public string StateText { get { return Done ? "已完成" : "待完成"; } }
        public string ToggleText { get { return Done ? "恢复" : "完成"; } }
        public string ToggleLabel { get { return (Done ? "恢复任务：" : "完成任务：") + Title; } }
        public string DeleteLabel { get { return "删除任务：" + Title; } }
    }

    // 所有 SQL 都集中在这里。界面只调用 Add / SetDone / Delete / List。
    public sealed class TaskStore : IDisposable
    {
        private IntPtr db;
        private const int Row = 100, Finished = 101;
        private static readonly IntPtr Transient = new IntPtr(-1);

        public TaskStore(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            int code = Native.sqlite3_open_v2(Utf8(path), out db, 2 | 4, IntPtr.Zero);
            if (code != 0) { string message = Error(); Dispose(); throw new IOException(message); }
            try
            {
                Check(Native.sqlite3_busy_timeout(db, 2000));
                Execute("PRAGMA synchronous=FULL;");
                Execute("CREATE TABLE IF NOT EXISTS tasks (id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL CHECK(length(trim(title))>0), done INTEGER NOT NULL DEFAULT 0 CHECK(done IN (0,1)), created_at TEXT NOT NULL);");
            }
            catch { Dispose(); throw; }
        }

        public List<TodoTask> List()
        {
            var result = new List<TodoTask>();
            IntPtr statement = Prepare("SELECT id,title,done,created_at FROM tasks ORDER BY done ASC,id DESC;");
            try
            {
                int code;
                while ((code = Native.sqlite3_step(statement)) == Row)
                    result.Add(new TodoTask { Id = Native.sqlite3_column_int64(statement, 0), Title = ReadText(statement, 1), Done = Native.sqlite3_column_int(statement, 2) == 1, CreatedAt = ReadText(statement, 3) });
                if (code != Finished) throw new IOException(Error());
            }
            finally { Native.sqlite3_finalize(statement); }
            return result;
        }

        public void Add(string title)
        {
            title = (title ?? "").Trim();
            if (title.Length == 0) throw new ArgumentException("请先输入任务标题，不能只输入空格。");
            if (title.Length > 120) throw new ArgumentException("任务标题最多 120 个字符。");
            IntPtr statement = Prepare("INSERT INTO tasks(title,done,created_at) VALUES(?1,0,?2);");
            try { BindText(statement, 1, title); BindText(statement, 2, DateTime.Now.ToString("yyyy-MM-dd HH:mm")); Commit(statement); }
            finally { Native.sqlite3_finalize(statement); }
        }

        public void SetDone(long id, bool done)
        {
            IntPtr statement = Prepare("UPDATE tasks SET done=?1 WHERE id=?2;");
            try { Check(Native.sqlite3_bind_int(statement, 1, done ? 1 : 0)); Check(Native.sqlite3_bind_int64(statement, 2, id)); Commit(statement); }
            finally { Native.sqlite3_finalize(statement); }
        }

        public void Delete(long id)
        {
            IntPtr statement = Prepare("DELETE FROM tasks WHERE id=?1;");
            try { Check(Native.sqlite3_bind_int64(statement, 1, id)); Commit(statement); }
            finally { Native.sqlite3_finalize(statement); }
        }

        private void Execute(string sql)
        {
            IntPtr statement = Prepare(sql);
            try { Commit(statement); } finally { Native.sqlite3_finalize(statement); }
        }
        private IntPtr Prepare(string sql)
        {
            IntPtr statement;
            Check(Native.sqlite3_prepare_v2(db, Utf8(sql), -1, out statement, IntPtr.Zero));
            return statement;
        }
        private void Commit(IntPtr statement)
        {
            if (Native.sqlite3_step(statement) != Finished) throw new IOException(Error());
        }
        private void BindText(IntPtr statement, int index, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Check(Native.sqlite3_bind_text(statement, index, bytes, bytes.Length, Transient));
        }
        private static byte[] Utf8(string value) { return Encoding.UTF8.GetBytes(value + "\0"); }
        private static string ReadText(IntPtr statement, int index)
        {
            IntPtr pointer = Native.sqlite3_column_text(statement, index);
            int size = Native.sqlite3_column_bytes(statement, index);
            if (pointer == IntPtr.Zero || size == 0) return "";
            var bytes = new byte[size]; Marshal.Copy(pointer, bytes, 0, size);
            return Encoding.UTF8.GetString(bytes);
        }
        private string Error()
        {
            IntPtr pointer = Native.sqlite3_errmsg(db);
            int size = 0; while (Marshal.ReadByte(pointer, size) != 0) size++;
            var bytes = new byte[size]; Marshal.Copy(pointer, bytes, 0, size);
            return "本地数据库操作失败：" + Encoding.UTF8.GetString(bytes);
        }
        private void Check(int code) { if (code != 0) throw new IOException(Error()); }
        public void Dispose() { if (db != IntPtr.Zero) { Native.sqlite3_close(db); db = IntPtr.Zero; } }

        // P/Invoke 类似 C++ 调用 DLL 中的函数；使用 Windows 已提供的 SQLite。
        private static class Native
        {
            private const string Dll = "winsqlite3.dll";
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2(byte[] name, out IntPtr db, int flags, IntPtr vfs);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_close(IntPtr db);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_busy_timeout(IntPtr db, int ms);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_errmsg(IntPtr db);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int size, out IntPtr statement, IntPtr tail);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_step(IntPtr statement);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(IntPtr statement);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int size, IntPtr destructor);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_int(IntPtr statement, int index, int value);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern long sqlite3_column_int64(IntPtr statement, int column);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_int(IntPtr statement, int column);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_bytes(IntPtr statement, int column);
        }
    }
}
