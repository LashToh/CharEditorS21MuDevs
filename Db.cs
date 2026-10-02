using System.Data;
using Microsoft.Data.SqlClient;

namespace MuBredaEditor;

/// <summary>Parameters are referenced in SQL as @p0, @p1, ... in the order given.</summary>
public sealed class Db(string connectionString)
{
    private SqlConnection Open()
    {
        var c = new SqlConnection(connectionString);
        c.Open();
        return c;
    }

    private static SqlCommand Cmd(SqlConnection c, SqlTransaction? tx, string sql, object?[] ps)
    {
        var cmd = new SqlCommand(sql, c, tx) { CommandTimeout = 30 };
        for (var i = 0; i < ps.Length; i++)
            cmd.Parameters.AddWithValue("@p" + i, ps[i] ?? DBNull.Value);
        return cmd;
    }

    public DataTable Query(string sql, params object?[] ps)
    {
        using var c = Open();
        using var cmd = Cmd(c, null, sql, ps);
        using var r = cmd.ExecuteReader();
        var t = new DataTable();
        t.Load(r);
        return t;
    }

    public DataRow? Row(string sql, params object?[] ps)
    {
        var t = Query(sql, ps);
        return t.Rows.Count > 0 ? t.Rows[0] : null;
    }

    public object? Scalar(string sql, params object?[] ps)
    {
        using var c = Open();
        using var cmd = Cmd(c, null, sql, ps);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    public int Exec(string sql, params object?[] ps)
    {
        using var c = Open();
        using var cmd = Cmd(c, null, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    public void Transaction(Action<Func<string, object?[], int>> body)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        try
        {
            body((sql, ps) =>
            {
                using var cmd = Cmd(c, tx, sql, ps);
                return cmd.ExecuteNonQuery();
            });
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public bool IsAccountOnline(string account) =>
        Convert.ToInt32(Scalar("SELECT ISNULL(MAX(CAST(ConnectStat AS int)),0) FROM MEMB_STAT WHERE memb___id=@p0", account) ?? 0) == 1;
}
