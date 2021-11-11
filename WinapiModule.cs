using Discord;
using Discord.Commands;
using Fizzler.Systems.HtmlAgilityPack;
using GLaDOSV3.Helpers;
using HtmlAgilityPack;
using Octokit;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GLaDOSV3.Module.Developers.WindbgConverter;

namespace GLaDOSV3.Module.Developers
{
    public class WinapiModule : ModuleBase<ShardedCommandContext>
    {
        private readonly Random rnd = new Random();

        [Command("wingdb", RunMode = RunMode.Async)]
        [Remarks("wingdb <dump>")]
        [Summary("Converts WinDbg \"dt\" structure dump to a C structure")]
        public async Task Wingdb([Remainder] string dump)
        {
            if (string.IsNullOrWhiteSpace(dump)) { await this.ReplyAsync("You can't convert something you don't have! Duh.");return;}
            if(!dump.StartsWith("kd>")) { await this.ReplyAsync("You must include the command you invoked as well!"); return; }

            try
            {
                var oof = new WindbgStructure(dump);
                await this.ReplyAsync($"Here's your structure!\n```cpp\n{oof.AsString(0)}\n```");
            } catch (Exception) {}
        }
        [Command("win32", RunMode = RunMode.Async)]
        [Remarks("win32 <winapi>")]
        [Summary("Winapi search!")]
        public async Task WinapiSearch([Remainder] string winapi)
        {
            Task typing = Context.Channel.TriggerTypingAsync();
            try
            {
                GitHubClient github = new GitHubClient(new ProductHeaderValue("GLaDOS_V3"))
                {
                    Credentials = new Credentials("dddbe430ea9eb99c131ebbb60b6c8e1507731496")
                };
                SearchCode[] searchResult =
                    (await
                         github.Search.SearchCode(new
                                                      SearchCodeRequest($"{winapi} repo:MicrosoftDocs/win32 repo:MicrosoftDocs/windows-driver-docs-ddi repo:MicrosoftDocs/windows-driver-docs",
                                                                        "MicrosoftDocs", "win32"))).Items.ToArray();
                if (searchResult.Length == 0)
                {
                    await this.ReplyAsync("Windows function not found!");
                    return;
                }

                var resultString = "";
                foreach (SearchCode gitResult in searchResult)
                {
                    System.Collections.Generic.IReadOnlyList<RepositoryContent> result =
                        await github.Repository.Content.GetAllContents(gitResult.Repository.Id, gitResult.Path);
                    var content = result[0].Content.ToLowerInvariant();
                    var index = content.IndexOf($"[**{winapi.ToLowerInvariant()}**]", StringComparison.Ordinal)
                                     + 3;
                    if (index <= 10)
                        index = content.IndexOf($">{winapi.ToLowerInvariant()}</a>", StringComparison.Ordinal) + 1;

                    if (index <= 10) continue;
                    Regex r = new Regex($"<a[^>]+href=[\"'](.*?)[\"']>{winapi.ToLowerInvariant()}<\\/a>",
                                        RegexOptions.Compiled | RegexOptions.CultureInvariant);
                    MatchCollection m = r.Matches(content);
                    if (m.Count != 0)
                    {
                        resultString = m[0].Groups[1].Value;
                        break;
                    }

                    resultString = result[0].Content[index..];
                    resultString = resultString[(resultString.IndexOf("**](", StringComparison.Ordinal) + 4)..];
                    if (resultString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        index = resultString.IndexOf(".aspx", StringComparison.Ordinal) + 5;
                        if (index >= 10) resultString = resultString[..index];
                        index = resultString.IndexOf("),", StringComparison.Ordinal);
                        if (index >= 10) resultString = resultString[..index];
                        index = resultString.IndexOf(") ", StringComparison.Ordinal);
                        if (index >= 10) resultString = resultString[..index];
                        index = resultString.IndexOf(',', StringComparison.Ordinal);
                        if (index >= 10) resultString = resultString[..index];
                        index = resultString.IndexOf('\n', StringComparison.Ordinal);
                        if (index >= 10) resultString = resultString[..index];

                    }
                    else
                    {
                        resultString = resultString[..resultString.IndexOf(')', StringComparison.Ordinal)];
                        resultString =
                            resultString[(resultString.IndexOf("/api/", StringComparison.Ordinal) + 5)..];
                        resultString = $"https://docs.microsoft.com/en-us/windows/win32/api/{resultString}";
                    }

                    break;
                }

                if (string.IsNullOrWhiteSpace(resultString))
                {
                    await this.ReplyAsync("Windows function not found!");
                    return;
                }

                HtmlDocument web = await new HtmlWeb().LoadFromWebAsync(resultString);

                var syntax = web.DocumentNode.QuerySelector("#main > pre > code")?.InnerText; ;
                EmbedBuilder builder = new EmbedBuilder
                {
                    Color = new Color(this.rnd.Next(256), this.rnd.Next(256), this.rnd.Next(256)), Footer =
                        new EmbedFooterBuilder
                        {
                            Text = $"Requested by {Context.User.Username}#{Context.User.Discriminator}",
                            IconUrl = Context.User.GetAvatarUrl()
                        },
                    Author = new EmbedAuthorBuilder()
                    {
                        IconUrl = "https://www.freepngimg.com/download/microsoft_windows/4-2-microsoft-windows-png-pic.png",
                        Name = $"Microsoft Docs",
                        Url = resultString
                    },
                    Description = await GetRequirements(web.DocumentNode)
                };
                builder.AddField("Syntax", $"```cpp\n{syntax ?? "fuck me error"}\n```");
                var embeds = Tools.SplitMessage((await this.GetParameters(web.DocumentNode)), 1024);
                for (var i = 0; i < embeds.Length; i++)
                {
                    builder.AddField((i == 0 ? "Parameters" : "\u200B"), embeds[i]);
                }
                {
                    var words = (syntax ?? "fuck me error").ReduceWhitespace().Replace("\n", "").Replace("\r", "").Split(' ');
                    words = words.Where(f => !f.StartsWith("__")).ToArray();
                    words = words.Where(f => !f.EndsWith("API")).ToArray();
                    var type = words[0];
                    var name = words[1][..^1];
                    var args = "";
                    for (var i = 2; i < words.Length; i++) args += $"{words[i]} ";
                    args = args[..^5];
                    builder.AddField("Typedef **(BETA)**", $"```cpp\ntypedef {type}(*{name}_t)({args});\n```");
                }
                await this.ReplyAsync(embed: builder.Build());
            }
            catch (ForbiddenException e) { await this.ReplyAsync(e.ToString()); }
            finally
            {
                typing.Dispose();
            }
        }
        #region Utils
        private async Task<string> GetParameters(HtmlNode document)
        {
            HtmlNode parameters = document.QuerySelector("#parameters");
            var response = "";
            var i = 0;
            while (parameters.NextSibling != null && parameters.NextSibling.Id != "return-value")
            {
                parameters = parameters.NextSibling;
                if (parameters.InnerHtml == "\n") continue;
                if (i++ % 2 == 0 && parameters.Name != "table") { response += $"**{parameters.InnerText}**: "; continue; }
                if (parameters.Name == "table")
                {
                    var fix = "";
                    foreach (HtmlNode row in parameters.SelectNodes("tr"))
                    {
                        foreach (HtmlNode cell in row.SelectNodes("th|td"))
                        {
                            if (cell.InnerHtml.ToLowerInvariant() == "meaning" || cell.InnerHtml.ToLowerInvariant() == "value") break;
                            if (cell.Attributes.Count == 1 && cell.Attributes.First().Value == "40%") fix += $"\n{await this.CleanupString(cell.InnerHtml)}\n";
                            else fix += $"{await this.CleanupString(cell.InnerHtml)}";
                        }
                    }
                    response += $"\n*{fix}*\n";
                }
                else response += $"{await this.CleanupString(parameters.InnerHtml)}\n";
            }
            return response;
        }
        private async Task<string> CleanupString(string fix)
        {
            fix = fix.Replace("*", "\\*").Replace("\n", " ").Trim();
            Regex r = new Regex("<\\s*a href=\"(.*?)\"[^>]*>(.*?)<\\s*\\/\\s*a>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            MatchCollection matches = r.Matches(fix);
            foreach (Match t in matches) fix = fix.Replace(t.Groups[0].Value, $"[{t.Groups[2]}](https://docs.microsoft.com/{t.Groups[1]})");
            fix = Regex.Replace(fix, "<a[^>]+id=\"(.*?)\"[^>]*>(.*?)</a>", "", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            r = new Regex("<p>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            matches = r.Matches(fix);
            foreach (Match t in matches) fix = fix.Replace(t.Groups[0].Value, $"\n{t.Groups[1]}");
            if (fix.StartsWith("<dl> <dt>"))
            {
                fix = fix[9..];
                fix = fix.Replace("</dt>", "");
                fix = fix.Replace("<dt>", "(").Replace(" </dl>", ")");
            }
            fix = fix.Replace("<code>", "``", StringComparison.OrdinalIgnoreCase).Replace("</code>", "``", StringComparison.OrdinalIgnoreCase);
            fix = fix.Replace("<i>", "*", StringComparison.OrdinalIgnoreCase).Replace("</i>", "*", StringComparison.OrdinalIgnoreCase);
            r = new Regex("<div(.*?)>(.*?)</div>", RegexOptions.Singleline | RegexOptions.Compiled);
            matches = r.Matches(fix);
            foreach (Match t in matches) fix = fix.Replace(t.Groups[0].Value, $"{t.Groups[2]}");
            r = new Regex("<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.Compiled);
            matches = r.Matches(fix);
            foreach (Match t in matches) fix = fix.Replace(t.Groups[0].Value, $"**{t.Groups[1]}**");
            return fix;
        }
        private static async Task<string> GetRequirements(HtmlNode document)
        {
            HtmlNode requirements = document.QuerySelector("#requirements");
            requirements = requirements.NextSibling.NextSibling;
            var stringReq = requirements.InnerText.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            var reqString = "";
            for (var i = 0; i < stringReq.Length; i++)
            {
                var t = stringReq[i];
                var start = t.LastIndexOf("[") + "[".Length;
                var end = t.IndexOf("]", start);
                var result = t.Remove((start <= 0 ? 0 : start), (end - start <= 0 ? 0 : end - start)).Replace(" []", "");
                if (i % 2 == 0) reqString += $"**{result}**: ";
                else reqString += $"{result}\n";
            }
            return reqString;
        }
#endregion
    }
}
