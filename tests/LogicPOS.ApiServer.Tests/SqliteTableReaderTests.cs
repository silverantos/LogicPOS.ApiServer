using LogicPOS.ApiServer.Endpoints;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Tests;

public class SqliteTableReaderTests
{
    [Theory]
    [InlineData("Terminals", "\"Terminals\"")]
    [InlineData("Quoted\"Table", "\"Quoted\"\"Table\"")]
    public void QuoteIdentifier_ReturnsProperlyDelimitedIdentifier(string identifier, string expected)
    {
        Assert.Equal(expected, SqliteTableReader.QuoteIdentifier(identifier));
    }

    [Fact]
    public async Task ReadTableAsync_CanReadTableWithEscapedIdentifier()
    {
        const string tableName = "Quoted\"Table";

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                CREATE TABLE {SqliteTableReader.QuoteIdentifier(tableName)} (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL
                );
                INSERT INTO {SqliteTableReader.QuoteIdentifier(tableName)} (Name) VALUES ('Mesa 1');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);

        var rows = await SqliteTableReader.ReadTableAsync(db, tableName, CancellationToken.None);
        var row = Assert.Single(rows);

        Assert.Equal("Mesa 1", row["Name"]);
    }
}
