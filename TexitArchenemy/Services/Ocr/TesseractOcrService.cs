using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TexitArchenemy.Services.Ocr;

public static class TesseractOcrService
{
    public static async Task<string> ReadTextAsync(byte[] imageBytes)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("tesseract", "stdin stdout -l eng")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        await process.StandardInput.BaseStream.WriteAsync(imageBytes);
        process.StandardInput.Close();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"tesseract exited {process.ExitCode}: {await stderrTask}");

        return await stdoutTask;
    }
}
