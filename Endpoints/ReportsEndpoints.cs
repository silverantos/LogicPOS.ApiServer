using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LogicPOS.ApiServer.Endpoints;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/reports").WithTags("Reports");

        reports.MapGet("/sales-for-day", async (HttpContext context, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var dayQuery = context.Request.Query["Day"].ToString();
            if (string.IsNullOrWhiteSpace(dayQuery))
            {
                dayQuery = context.Request.Query["day"].ToString();
            }

            var targetDate = DateTime.TryParse(dayQuery, out var parsedDate)
                ? parsedDate.Date
                : DateTime.Today;

            var documents = await LoadActiveSalesDocumentsAsync(db, cancellationToken);

            decimal dayTotal = 0m;
            decimal monthTotal = 0m;
            decimal yearTotal = 0m;

            foreach (var doc in documents)
            {
                if (doc.CreatedAt.Year == targetDate.Year)
                {
                    yearTotal += doc.TotalFinal;

                    if (doc.CreatedAt.Month == targetDate.Month)
                    {
                        monthTotal += doc.TotalFinal;

                        if (doc.CreatedAt.Day == targetDate.Day)
                        {
                            dayTotal += doc.TotalFinal;
                        }
                    }
                }
            }

            return Results.Ok(new
            {
                day = targetDate,
                dayTotal = Math.Round(dayTotal, 2),
                monthTotal = Math.Round(monthTotal, 2),
                yearTotal = Math.Round(yearTotal, 2)
            });
        })
        .WithName("GetSalesTotalForDay");

        reports.MapGet("/monthly-sales", async (HttpContext context, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var yearQuery = context.Request.Query["year"].ToString();
            if (string.IsNullOrWhiteSpace(yearQuery))
            {
                yearQuery = context.Request.Query["Year"].ToString();
            }

            var targetYear = int.TryParse(yearQuery, out var parsedYear) && parsedYear > 1900
                ? parsedYear
                : DateTime.Today.Year;

            var documents = await LoadActiveSalesDocumentsAsync(db, cancellationToken);

            var years = documents.Select(d => d.CreatedAt.Year).Distinct().OrderBy(y => y).ToList();
            if (!years.Contains(targetYear))
            {
                years.Add(targetYear);
                years.Sort();
            }

            var sales = new List<object>();
            for (var month = 1; month <= 12; month++)
            {
                var monthDocs = documents.Where(d => d.CreatedAt.Year == targetYear && d.CreatedAt.Month == month).ToList();
                var netTotal = monthDocs.Sum(d => d.TotalNet);
                var finalTotal = monthDocs.Sum(d => d.TotalFinal);

                sales.Add(new
                {
                    month,
                    netTotal = Math.Round(netTotal, 2),
                    finalTotal = Math.Round(finalTotal, 2)
                });
            }

            return Results.Ok(new
            {
                year = targetYear,
                years,
                sales
            });
        })
        .WithName("GetMonthlySalesReportData");

        foreach (var reportPath in MissingPdfReportPaths)
        {
            app.MapGet(reportPath, async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var documentCount = 0;
                try
                {
                    documentCount = (await SqliteTableReader.ReadTableAsync(db, "Documents", cancellationToken)).Count;
                }
                catch (InvalidOperationException)
                {
                }

                var pdf = CreatePdf(reportPath, documentCount);
                return Results.File(pdf, "application/pdf", "logicpos-report.pdf");
            });
        }
    }

    private static readonly string[] MissingPdfReportPaths =
    {
        "/reports/system-audits/pdf", "/reports/stock-movement/pdf", "/reports/stock/pdf",
        "/reports/stock-by-supplier/pdf", "/reports/stock-by-article/pdf", "/reports/stock-by-article-gain/pdf",
        "/reports/sales-by-vatrate-group/detailed/pdf", "/reports/sales-by-vatrate-and-articletype/pdf",
        "/reports/sales-by-vatrate-and-articleclass/pdf", "/reports/sales-by-subfamily/detailed/pdf",
        "/reports/sales-by-paymentmethod/pdf", "/reports/sales-by-payment-method/detailed/pdf",
        "/reports/sales-by-paymentcondition/pdf", "/reports/sales-by-payment-condition/detailed/pdf",
        "/reports/sales-by-family/detailed/pdf", "/reports/sales-by-employee/pdf",
        "/reports/sales-by-employee/detailed/pdf", "/reports/sales-by-document-type/pdf",
        "/reports/sales-by-document-type/detailed/pdf", "/reports/sales-by-date/pdf",
        "/reports/sales-by-document-date/detailed/pdf", "/reports/sales-by-customer/pdf",
        "/reports/sales-by-customer/detailed/pdf", "/reports/sales-by-currency/pdf",
        "/reports/sales-by-currency/detailed/pdf", "/reports/sales-by-country/pdf",
        "/reports/sales-by-country/detailed/pdf", "/reports/sales-by-terminal/pdf",
        "/reports/sales-by-terminal/detailed/pdf", "/reports/sales-by-table/detailed/pdf",
        "/reports/sales-by-place/detailed/pdf", "/reports/sales-by-commission/pdf",
        "/reports/deleted-orders/pdf", "/reports/customers/suppliers/pdf", "/reports/customers/list/pdf",
        "/reports/customers/current-account/summary/pdf", "/reports/customers/{customerId}/current-account/pdf",
        "/reports/company/billing/pdf", "/reports/articles/total-sold/pdf", "/reports/articles/pdf"
    };

    private static byte[] CreatePdf(string reportPath, int documentCount)
    {
        var title = EscapePdfText(reportPath);
        var body = $"BT /F1 12 Tf 72 740 Td ({title}) Tj 0 -24 Td (Documents available: {documentCount}) Tj 0 -24 Td (Generated by LogicPOS API) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {body.Length} >>\nstream\n{body}\nendstream"
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write("%PDF-1.4\n");
        writer.Flush();
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(stream.Position);
            writer.Write($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            writer.Flush();
        }
        var xrefOffset = stream.Position;
        writer.Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++) writer.Write($"{offsets[index]:D10} 00000 n \n");
        writer.Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        writer.Flush();
        return stream.ToArray();
    }

    private static string EscapePdfText(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static async Task<List<SalesDocument>> LoadActiveSalesDocumentsAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var list = new List<SalesDocument>();

        try
        {
            var rows = await SqliteTableReader.ReadTableAsync(db, "Documents", cancellationToken);
            foreach (var row in rows)
            {
                if (IsTruthy(row, "IsDraft") || IsTruthy(row, "IsDeleted"))
                {
                    continue;
                }

                var status = Convert.ToString(row.GetValueOrDefault("Status"));
                if (string.Equals(status, "A", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!row.TryGetValue("CreatedAt", out var createdVal) || createdVal is null)
                {
                    continue;
                }

                DateTime createdAt;
                if (createdVal is DateTime dt)
                {
                    createdAt = dt;
                }
                else if (!DateTime.TryParse(createdVal.ToString(), out createdAt))
                {
                    continue;
                }

                var docType = Convert.ToString(row.GetValueOrDefault("Type")) ?? string.Empty;
                var multiplier = string.Equals(docType, "NC", StringComparison.OrdinalIgnoreCase) ? -1m : 1m;

                var totalFinal = multiplier * Convert.ToDecimal(row.GetValueOrDefault("TotalFinal") ?? 0m);
                var totalNet = multiplier * Convert.ToDecimal(row.GetValueOrDefault("TotalNet") ?? 0m);

                list.Add(new SalesDocument(createdAt, totalFinal, totalNet, docType));
            }
        }
        catch
        {
            // Return whatever documents were loaded or an empty list
        }

        return list;
    }

    private static bool IsTruthy(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            byte by => by != 0,
            short s => s != 0,
            int i => i != 0,
            long l => l != 0,
            string str when int.TryParse(str, out var num) => num != 0,
            _ => bool.TryParse(Convert.ToString(value), out var bVal) && bVal
        };
    }

    private sealed record SalesDocument(DateTime CreatedAt, decimal TotalFinal, decimal TotalNet, string DocType);
}
