using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace CaForecast.Data;

public class MoexIssService
{
    private const int MaxRangeDays = 365 * 30;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public CsvImportedData ImportDailyHistory(string secId, DateTime from, DateTime till, string boardId = "TQBR")
    {
        if (string.IsNullOrWhiteSpace(secId))
        {
            throw new ArgumentException("Требуется тикер бумаги.", nameof(secId));
        }

        if (string.IsNullOrWhiteSpace(boardId))
        {
            throw new ArgumentException("Требуется код режима торгов.", nameof(boardId));
        }

        if (till < from)
        {
            throw new ArgumentException("Дата окончания периода должна быть не раньше даты начала.");
        }

        var rangeDays = (till.Date - from.Date).TotalDays;
        if (rangeDays > MaxRangeDays)
        {
            throw new ArgumentException("Слишком большой диапазон дат. Выберите период не более 30 лет.");
        }

        var prices = new List<double>();
        var dates = new List<DateTime?>();
        var start = 0;

        while (true)
        {
            var url = BuildHistoryUrl(secId, boardId, from, till, start);

            HttpResponseMessage response;
            try
            {
                response = HttpClient.GetAsync(url).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException ex)
            {
                throw new TimeoutException(
                    "MOEX ISS не ответил вовремя. Попробуйте сузить диапазон дат или повторить позже.",
                    ex);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    "Не удалось подключиться к MOEX ISS. Проверьте подключение к интернету.",
                    ex);
            }

            using (response)
            {
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                {
                    throw new InvalidOperationException(
                        $"MOEX ISS не нашел данные для тикера '{secId}' и режима торгов '{boardId}'. Проверьте параметры.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"MOEX ISS вернул ошибку HTTP {(int)response.StatusCode} ({response.StatusCode}).");
                }

                using var stream = response.Content.ReadAsStream();
                using var document = JsonDocument.Parse(stream);

                if (!document.RootElement.TryGetProperty("history", out var historyElement))
                {
                    throw new InvalidOperationException("Ответ MOEX ISS не содержит секцию history.");
                }

                var dateIndex = -1;
                var closeIndex = -1;
                if (historyElement.TryGetProperty("columns", out var columnsElement))
                {
                    var index = 0;
                    foreach (var column in columnsElement.EnumerateArray())
                    {
                        var name = column.GetString() ?? string.Empty;
                        if (name.Equals("TRADEDATE", StringComparison.OrdinalIgnoreCase))
                        {
                            dateIndex = index;
                        }
                        else if (name.Equals("CLOSE", StringComparison.OrdinalIgnoreCase))
                        {
                            closeIndex = index;
                        }

                        index++;
                    }
                }

                if (dateIndex < 0 || closeIndex < 0)
                {
                    throw new InvalidOperationException("В ответе MOEX ISS не найдены поля TRADEDATE/CLOSE.");
                }

                if (!historyElement.TryGetProperty("data", out var dataElement))
                {
                    throw new InvalidOperationException("Ответ MOEX ISS не содержит данные history.data.");
                }

                var pageRows = 0;
                foreach (var row in dataElement.EnumerateArray())
                {
                    pageRows++;
                    if (row.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var rowValues = row.EnumerateArray().ToArray();
                    if (rowValues.Length <= Math.Max(dateIndex, closeIndex))
                    {
                        continue;
                    }

                    var closeValue = rowValues[closeIndex];
                    var dateValue = rowValues[dateIndex];

                    if (!TryParseDouble(closeValue, out var close))
                    {
                        continue;
                    }

                    if (!TryParseDate(dateValue, out var tradeDate))
                    {
                        continue;
                    }

                    prices.Add(close);
                    dates.Add(tradeDate);
                }

                if (pageRows == 0)
                {
                    break;
                }

                start += pageRows;
            }
        }

        if (prices.Count < 2)
        {
            throw new InvalidOperationException(
                $"По выбранным параметрам найдено недостаточно данных (меньше двух цен). Проверьте тикер '{secId}', режим торгов '{boardId}' и диапазон дат.");
        }

        return new CsvImportedData
        {
            ClosePrices = prices,
            Dates = dates
        };
    }

    private static string BuildHistoryUrl(string secId, string boardId, DateTime from, DateTime till, int start)
    {
        return
            $"https://iss.moex.com/iss/history/engines/stock/markets/shares/boards/{Uri.EscapeDataString(boardId)}/securities/{Uri.EscapeDataString(secId)}.json" +
            $"?from={from:yyyy-MM-dd}&till={till:yyyy-MM-dd}&start={start}&iss.meta=off";
    }

    private static bool TryParseDate(JsonElement element, out DateTime value)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                {
                    return true;
                }

                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryParseDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }

                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
