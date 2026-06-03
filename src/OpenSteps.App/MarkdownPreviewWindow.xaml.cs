using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using OpenSteps.Core.Models;

namespace OpenSteps.App;

public partial class MarkdownPreviewWindow : Window
{
    private readonly string _markdown;

    public MarkdownPreviewWindow(string markdown, RecordingSession session)
    {
        _markdown = markdown;
        InitializeComponent();
        PreviewBrowser.NavigateToString(BuildHtml(session));
    }

    private void CopyMarkdown_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_markdown);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string BuildHtml(RecordingSession session)
    {
        var body = new StringBuilder();
        body.AppendLine($"<h1>{Html(session.Title)}</h1>");
        body.AppendLine("<p>Created with OpenSteps.</p>");

        for (var i = 0; i < session.Steps.Count; i++)
        {
            var step = session.Steps[i];
            body.AppendLine($"<h2>Step {i + 1}: {Html(step.DisplayTitle)}</h2>");

            var screenshotPath = GetPreviewScreenshotPath(step);
            if (!string.IsNullOrWhiteSpace(screenshotPath) && File.Exists(screenshotPath))
            {
                body.AppendLine($"<p><img alt=\"Step {i + 1}\" src=\"{new Uri(screenshotPath).AbsoluteUri}\" /></p>");
            }

            if (!string.IsNullOrWhiteSpace(step.UserDescription))
            {
                body.AppendLine($"<p>{Html(step.UserDescription).ReplaceLineEndings("<br>")}</p>");
            }
        }

        return $$"""
        <!doctype html>
        <html>
        <head>
          <meta http-equiv="X-UA-Compatible" content="IE=edge" />
          <meta charset="utf-8" />
          <style>
            body {
              margin: 0;
              background: #ffffff;
              color: #24292f;
              font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
              font-size: 16px;
              line-height: 1.5;
            }
            .markdown-body {
              box-sizing: border-box;
              min-width: 200px;
              max-width: 980px;
              margin: 0 auto;
              padding: 32px;
            }
            h1, h2 {
              margin-top: 24px;
              margin-bottom: 16px;
              font-weight: 600;
              line-height: 1.25;
              border-bottom: 1px solid #d8dee4;
              padding-bottom: .3em;
            }
            h1 {
              font-size: 2em;
            }
            h2 {
              font-size: 1.5em;
            }
            p {
              margin-top: 0;
              margin-bottom: 16px;
            }
            img {
              max-width: 100%;
              box-sizing: content-box;
              background-color: #ffffff;
              border: 1px solid #d8dee4;
              border-radius: 6px;
            }
          </style>
        </head>
        <body>
          <main class="markdown-body">
            {{body}}
          </main>
        </body>
        </html>
        """;
    }

    private static string Html(string? value)
    {
        return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "Untitled OpenSteps Guide" : value.Trim());
    }

    private static string? GetPreviewScreenshotPath(RecordedStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.EditedScreenshotPath) && File.Exists(step.EditedScreenshotPath))
        {
            return step.EditedScreenshotPath;
        }

        return step.ScreenshotPath;
    }
}
