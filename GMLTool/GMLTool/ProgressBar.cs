using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMLTool
{
    public class ProgressBar : IDisposable
    {
        private const int width = 35;
        private readonly char[] phases = new char[] { ' ', '▏', '▎', '▍', '▌', '▋', '▊', '▉', '█' };
        private readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string animation = @"|/-\";

        private readonly Timer timer;

        private double currentProgress = 0;
        private string currentTemplate = "{Bar} {Progress} {Icon}";
        private string currentText = string.Empty;
        private bool disposed = false;
        private int animationIndex = 0;

        public ProgressBar()
        {
            timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!Console.IsOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Template(string value)
        {
            Interlocked.Exchange(ref currentTemplate, value);
        }

        public void Report(double value)
        {
            // Make sure value is in [0..1] range
            value = Math.Max(0, Math.Min(1, value));
            Interlocked.Exchange(ref currentProgress, value);
        }

        private void TimerHandler(object state)
        {
            lock (timer)
            {
                if (disposed) return;

                int nphases = phases.Length;

                double filledLen = width * currentProgress;
                int nFull = (int)filledLen;                         // Number of full chars
                int phase = (int)((filledLen - nFull) * nphases);   // Phase of last char
                int nEmpty = width - nFull;                         // Number of empty chars

                string bar = $"|{new string(phases[phases.Length - 1], nFull)}{phases[phase]}{new string(phases[0], nEmpty)}|";
                string progress = $"{currentProgress:P}";
                string icon = $"{animation[animationIndex++ % animation.Length]}";

                string text = currentTemplate.Replace("{Bar}", bar).Replace("{Progress}", progress).Replace("{Icon}", icon);

                UpdateText(text);

                ResetTimer();
            }
        }

        private void UpdateText(string text)
        {
            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength])
            {
                commonPrefixLength++;
            }

            // Backtrack to the first differing character
            StringBuilder outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = currentText.Length - text.Length;
            if (overlapCount > 0)
            {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }

            Console.Write(outputBuilder);
            currentText = text;
        }

        private void ResetTimer()
        {
            timer.Change(animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (timer)
            {
                disposed = true;
                UpdateText(string.Empty);
            }
        }

    }
}
