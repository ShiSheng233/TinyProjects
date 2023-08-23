using System.Collections.Concurrent;
using CliWrap;
using CliWrap.Buffered;


namespace EncodeFlow;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private string _ffmpegBinary;
    private string _ffmpegArguments0;
    private string _ffmpegArguments1;
    private string _targetFolder;
    private string _outputFolder;
    private string _filter;
    private readonly FileSystemWatcher _fileWatcher;

    // A queue for the files to be encoded
    private readonly ConcurrentQueue<string> _encodingQueue = new ConcurrentQueue<string>();

    // A queue for the files that failed to encode and need to be retried
    private readonly ConcurrentQueue<string> _retryQueue = new ConcurrentQueue<string>();
    
    private static bool FileIsReady(string path)
    {
        // One way of telling the file is not ready is by checking if it can be opened for exclusive access.
        try
        {
            using (var inputStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                return inputStream.Length > 0;
            }
        }
        catch (Exception)
        {
            // If the file cannot be opened, it means that the file is still being written.
            return false;
        }
    }

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger = logger;
        _ffmpegBinary = config.GetSection("FFmpeg:Binary").Value;
        _ffmpegArguments0 = config.GetSection("FFmpeg:Arguments0").Value;
        _ffmpegArguments1 = config.GetSection("FFmpeg:Arguments1").Value;
        _targetFolder = config.GetSection("EncodeFlow:TargetFolder").Value;
        _outputFolder = config.GetSection("EncodeFlow:OutputFolder").Value;
        _filter = config.GetSection("EncodeFlow:Filter").Value;

        _logger.LogInformation(
            "FFmpeg binary: {ffmpegBinary}\n      FFmpeg arguments0: {ffmpegArgument0}\n      FFmpeg arguments1: {ffmpegArguments1}\n      Target folder: {targetFolder}\n      Output folder: {outputFolder}\n      Filter: {filter}",
            _ffmpegBinary,_ffmpegArguments0, _ffmpegArguments1, _targetFolder, _outputFolder, _filter);

        _fileWatcher = new FileSystemWatcher(_targetFolder)
        {
            NotifyFilter = NotifyFilters.FileName,
            Filter = _filter
        };

        // And update the file created event handler
        _fileWatcher.Created += async (sender, e) =>
        {
            // File created event handler
            _logger.LogInformation("{file}: detected", Path.GetFileName(e.FullPath));
    
            // Wait until the file is ready
            while (!FileIsReady(e.FullPath))
            {
                await Task.Delay(500);
            }

            _encodingQueue.Enqueue(e.FullPath);
            _logger.LogInformation("{queueLength} file(s) in queue", _encodingQueue.Count);
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start watching the directory
        _fileWatcher.EnableRaisingEvents = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            string file;

            // Check if there are any files to be encoded
            if (!_encodingQueue.IsEmpty && _encodingQueue.TryDequeue(out file))
            {
                await EncodeFile(file);
            }
            // Check if there are any files that need to be retried
            else if (!_retryQueue.IsEmpty && _retryQueue.TryDequeue(out file))
            {
                await EncodeFile(file);
            }
            else
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        // Stop watching the directory
        _fileWatcher.EnableRaisingEvents = false;
    }

    private async Task EncodeFile(string file)
    {
        var startTime = DateTimeOffset.Now;
        _logger.LogInformation("{file}: Starting encoding at {time}", Path.GetFileName(file), startTime);

        try
        {
            var cmd = GetCommand(_ffmpegBinary, _ffmpegArguments0, _ffmpegArguments1, file, _outputFolder);
            var result = await cmd.ExecuteBufferedAsync();

            HandleResult(result, file, startTime, DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing FFmpeg command");
        }
    }
    
    private Command GetCommand(string ffmpegBinary, string arguments0, string arguments1, string file, string outputFolder)
    {
        if (arguments0 == "" && arguments1=="")
        {
            return Cli.Wrap(ffmpegBinary)
                .WithArguments(new[] {"-i"}.Concat(new[] {file}).Concat(new[] { "-y" }).Concat(new[] {Path.Combine(outputFolder, Path.GetFileName(file))}));
        }
        else
        {
            return Cli.Wrap(ffmpegBinary)
                .WithArguments(arguments0.Split(' ').Concat(new[] {"-i"}.Concat(new[] {file}).Concat(new[] { "-y" }).Concat(arguments1.Split(' ')).Concat(new[] {Path.Combine(outputFolder, Path.GetFileName(file))})));
        }
    }

    private void HandleResult(BufferedCommandResult result, string file, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        _logger.LogInformation("{file}: Finished encoding at {time}", Path.GetFileName(file), endTime);

        // Check if FFmpeg ran successfully
        if (result.ExitCode == 0)
        {
            // Add to the database
            var encodingTime = endTime - startTime;

            _logger.LogInformation("{file}: Encode cost {time}", Path.GetFileName(file), encodingTime);
        }
        else
        {
            _logger.LogError($"FFmpeg exited with code: {result.ExitCode}\n      Error output: {result.StandardError}");
        }
    }
}