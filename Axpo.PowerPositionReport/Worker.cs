namespace Axpo.PowerPositionReport;

public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var intervalMinutes = _configuration.GetValue<int?>("IntervalMinutes") ?? 5;
        var configuredOutputFolder = _configuration.GetValue<string>("OutputFolder") ?? "output";
        var outputFolder = Path.IsPathRooted(configuredOutputFolder)
            ? configuredOutputFolder
            : Path.Combine(AppContext.BaseDirectory, configuredOutputFolder);

        Directory.CreateDirectory(outputFolder);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await GeneratorReportAsync(outputFolder, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while generating power position report.");
            }

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), cancellationToken);
        }
    }

    private async Task GeneratorReportAsync(string outputFolder, CancellationToken cancellationToken)
    {
        var tradeDate = DateTime.Today;
        _logger.LogInformation("Generating report for trade date: {TradeDate}", tradeDate.ToString("yyyy-MM-dd"));

        var powerService = new PowerService();
        var trades = (await powerService.GetTradesAsync(tradeDate)).ToList();

        var rows = new List<string> { "Local Time,Volume" };

        for (int period = 1; period <= 24; period++)
        {
            var totalVolume = trades.Sum(trade => trade.Periods[period - 1].Volume);
            var localTime = tradeDate.Date.AddHours(period - 1).AddHours(-1).ToString("HH:mm");

            rows.Add($"{localTime}, {Math.Round(totalVolume, 2)}");

            var fileName = $"PowerPosition_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            var filePath = Path.Combine(outputFolder, fileName);

            await File.WriteAllLinesAsync(filePath, rows, cancellationToken);
            _logger.LogInformation("Report generated successfully: {FilePath}", filePath);
        }
    }
}
