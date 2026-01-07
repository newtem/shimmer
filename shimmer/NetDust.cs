using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NetDust
{
    public class RuntimeContext
    {
        public Dictionary<string, string> Vars { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> Nums { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> Slots { get; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        public List<string> Log { get; } = new List<string>();

        public Dictionary<string, string> ExternalInfo { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"user.name", "newtem"},
            {"user.id", "2"},
            {"user.role", "dev"}
        };

        public void AddLog(string line)
        {
            Log.Add($"{DateTime.Now:HH:mm:ss} - {line}");
        }
    }

    // Parser + Runner
    public class Engine
    {
        private static readonly Random RNG = new Random();
        private bool _inRoom = false;
        private string _curRoom = null;

        public RuntimeContext ExecuteScript(string script)
        {
            var ctx = new RuntimeContext();
            var lines = script.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // comment
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                try
                {
                    ProcessLine(line, ctx);
                }
                catch (Exception ex)
                {
                    ctx.AddLog($"Error executing '{line}': {ex.Message}");
                }
            }

            return ctx;
        }

        private void ProcessLine(string line, RuntimeContext ctx)
        {
            bool found = false;

            // code start
            if (line.Equals("code start", StringComparison.OrdinalIgnoreCase))
            {
                ctx.AddLog("code starts here!");
                found = true;
            }
            else
            {
                // bring
                Match bringMatch = Regex.Match(line, @"^bring\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                if (bringMatch.Success)
                {
                    ctx.AddLog($"BRING library recognized: {bringMatch.Groups[1].Value.Trim()}");
                    found = true;
                }
                else
                {
                    // var detection
                    Match varMatch = Regex.Match(line, @"^var\s+([A-Za-z_][A-Za-z0-9_]*)\s*(\{set\s*(.+)\})?$", RegexOptions.IgnoreCase);
                    if (varMatch.Success)
                    {
                        var name = varMatch.Groups[1].Value;
                        var val = varMatch.Groups[3].Success ? varMatch.Groups[3].Value.Trim().Trim('"') : "";
                        ctx.Vars[name] = val;
                        ctx.AddLog($"var {name} = \"{val}\"");
                        found = true;
                    }
                    else
                    {
                        // num name{set number}
                        Match numMatch = Regex.Match(line, @"^num\s+([A-Za-z_][A-Za-z0-9_]*)\s*(\{set\s*([0-9\.\-]+)\})?$", RegexOptions.IgnoreCase);
                        if (numMatch.Success)
                        {
                            var name = numMatch.Groups[1].Value;
                            double val = 0;
                            if (numMatch.Groups[3].Success) double.TryParse(numMatch.Groups[3].Value, out val);
                            ctx.Nums[name] = val;
                            ctx.AddLog($"num {name} = {val}");
                            found = true;
                        }
                        else
                        {
                            // simple set: set var = value
                            // it's Net Dust 2.2 syntax
                            // 2.3 Ex: set <var> from <data>
                            Match setMatch = Regex.Match(line, @"^set\s+([A-Za-z0-9_\.]+)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
                            if (setMatch.Success)
                            {
                                var key = setMatch.Groups[1].Value;
                                var rawVal = setMatch.Groups[2].Value.Trim().Trim('"');

                                if (ctx.Vars.ContainsKey(key))
                                {
                                    ctx.Vars[key] = rawVal;
                                    ctx.AddLog($"set var {key} = {rawVal}");
                                }
                                else if (ctx.Nums.ContainsKey(key) && double.TryParse(rawVal, out var num))
                                {
                                    ctx.Nums[key] = num;
                                    ctx.AddLog($"set num {key} = {num}");
                                }
                                else
                                {
                                    ctx.Vars[key] = rawVal;
                                    ctx.AddLog($"set (fallback) {key} = {rawVal}");
                                }
                                found = true;
                            }
                            else
                            {
                                // print(varName) or print("text", varName)
                                Match printMatch = Regex.Match(line, @"^print(?:\.([a-zA-Z0-9_]+))?\s*\(\s*(.*)\s*\)\s*;?$", RegexOptions.IgnoreCase);
                                if (printMatch.Success)
                                {
                                    var mode = printMatch.Groups[1].Value;
                                    var payload = printMatch.Groups[2].Value.Trim();
                                    ctx.AddLog($"PRINT{(string.IsNullOrEmpty(mode) ? "" : "." + mode)}: {ReplaceVarsInText(payload, ctx)}");
                                    found = true;
                                }
                                else
                                {
                                    // get = nf / uid  and variants
                                    // get = nf is only old Net Dust support..
                                    Match getMatch = Regex.Match(line, @"^get\s*=\s*([A-Za-z0-9_\-\.]+)\s*(?:<\s*([A-Za-z0-9_\-\.]+)\s*>)?$", RegexOptions.IgnoreCase);
                                    if (getMatch.Success)
                                    {
                                        var target = getMatch.Groups[1].Value;
                                        var arg = getMatch.Groups[2].Success ? getMatch.Groups[2].Value : null;

                                        if (ctx.ExternalInfo.TryGetValue(target, out var extVal))
                                            ctx.AddLog($"GET external {target} => {extVal}");
                                        else if (Regex.IsMatch(target, "nf|rk|rk/slot|nf/"))
                                            ctx.AddLog($"GET simulated: {target} => sample_result_for_{Regex.Replace(target, @"[^\w\.]", "_")}");
                                        else
                                            ctx.AddLog($"GET unknown target: {target}");

                                        if (arg != null) ctx.AddLog($"GET argument: {arg}");
                                        found = true;
                                    }
                                    else
                                    {
                                        // find ex:
                                        // find = nf / uid = 2 (Net Dust 2.2)
                                        // 2.3: set <var> from find <path> where <field> = <value>
                                        Match findMatch = Regex.Match(line, @"^find\s*=\s*([^\=]+)(?:\=\s*(.+))?$", RegexOptions.IgnoreCase);
                                        if (findMatch.Success)
                                        {
                                            var target = findMatch.Groups[1].Value.Trim();
                                            var query = findMatch.Groups[2].Success ? findMatch.Groups[2].Value.Trim() : "";
                                            ctx.AddLog($"FIND {target} -> query '{query}' => found simulated_result");
                                            found = true;
                                        }
                                        else
                                        {
                                            // Net Duwst 2.2 syntax
                                            // 2.3: make <object> at <path> as <name>
                                            Match makeMatch = Regex.Match(line, @"^make\s*/\s*new\s*=\s*([A-Za-z0-9_\.]+)\s*(\{([^\}]*)\})?\s*=\s*(""([^""]*)""|([A-Za-z0-9_\.]+))", RegexOptions.IgnoreCase);
                                            if (makeMatch.Success)
                                            {
                                                var type = makeMatch.Groups[1].Value;
                                                var props = makeMatch.Groups[3].Value;
                                                var val = makeMatch.Groups[5].Success ? makeMatch.Groups[5].Value : makeMatch.Groups[6].Value;
                                                ctx.AddLog($"MAKE new {type} {props} = {val}");
                                                found = true;
                                            }
                                            else
                                            {
                                                // Net Dust 2.2 syntax
                                                // 2.3: write <target> = "<text>"
                                                Match writeMatch = Regex.Match(line, @"^write\s*/\s*old\s*=\s*([A-Za-z0-9_\.]+)\s*=\s*v?(""([^""]*)""|(.+))", RegexOptions.IgnoreCase);
                                                if (writeMatch.Success)
                                                {
                                                    var target = writeMatch.Groups[1].Value;
                                                    var text = writeMatch.Groups[3].Success ? writeMatch.Groups[3].Value : writeMatch.Groups[4].Value;
                                                    ctx.AddLog($"WRITE to {target}: {ReplaceVarsInText(text, ctx)}");
                                                    found = true;
                                                }
                                                else
                                                {
                                                    // recognize = .rpp  or file = " C:\path "
                                                    Match recogMatch = Regex.Match(line, @"^recognize\s*=\s*(.+)$", RegexOptions.IgnoreCase);
                                                    if (recogMatch.Success)
                                                    {
                                                        ctx.AddLog($"RECOGNIZE {recogMatch.Groups[1].Value.Trim()} (simulated)");
                                                        found = true;
                                                    }
                                                    else
                                                    {
                                                        // room end; like func in C++
                                                        if (line.StartsWith("room ", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(line, @"^end;?$", RegexOptions.IgnoreCase))
                                                        {
                                                            if (!_inRoom)
                                                            {
                                                                var m = Regex.Match(line, @"^room\s+([A-Za-z0-9_]+)(\{([^\}]*)\})?[:;]?$", RegexOptions.IgnoreCase);
                                                                if (m.Success)
                                                                {
                                                                    _inRoom = true;
                                                                    _curRoom = m.Groups[1].Value;
                                                                    ctx.AddLog($"ROOM enter {_curRoom} (vars: {m.Groups[3].Value})");
                                                                }
                                                            }
                                                            else if (Regex.IsMatch(line, @"^end;?$", RegexOptions.IgnoreCase))
                                                            {
                                                                ctx.AddLog($"ROOM {_curRoom} closed");
                                                                _inRoom = false;
                                                                _curRoom = null;
                                                            }
                                                            found = true;
                                                        }
                                                        else
                                                        {
                                                            // range is random
                                                            Match rangeMatch = Regex.Match(line, @"^range\(\s*([A-Za-z0-9_]+)\s*=\s*([0-9]+)\s*,\s*\1\s*>\s*([0-9]+)\s*\)\s*$", RegexOptions.IgnoreCase);
                                                            if (rangeMatch.Success)
                                                            {
                                                                ctx.AddLog($"RANGE {rangeMatch.Groups[1].Value} from {rangeMatch.Groups[2].Value} to {rangeMatch.Groups[3].Value} (simulated)");
                                                                found = true;
                                                            }
                                                            else
                                                            {
                                                                // for comdust lib
                                                                Match randomMatch = Regex.Match(line, @"^cd\.rd\(\s*([A-Za-z0-9_]+)\s*=\s*([0-9]+)~([0-9]+)\s*\)\s*$", RegexOptions.IgnoreCase);
                                                                if (randomMatch.Success)
                                                                {
                                                                    var name = randomMatch.Groups[1].Value;
                                                                    int min = int.Parse(randomMatch.Groups[2].Value);
                                                                    int max = int.Parse(randomMatch.Groups[3].Value);
                                                                    int result = RNG.Next(min, max + 1);
                                                                    ctx.Nums[name] = result;
                                                                    ctx.AddLog($"RANDOM {name} = {result}");
                                                                    found = true;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!found)
            {
                ctx.AddLog($"Unknown or unhandled line: {line}");
            }
        }

        private string ReplaceVarsInText(string payload, RuntimeContext ctx)
        {
            var result = payload;

            // <name> replacement
            result = Regex.Replace(result, @"<\s*([A-Za-z0-9_\.]+)\s*>", m =>
            {
                var k = m.Groups[1].Value;
                if (ctx.Vars.TryGetValue(k, out var v)) return v;
                if (ctx.ExternalInfo.TryGetValue(k, out var ev)) return ev;
                if (ctx.Nums.TryGetValue(k, out var nv)) return nv.ToString();
                return $"<{k}>";
            });

            // "text" var replacement
            result = Regex.Replace(result, "\"([^\"]*)\"\\s*([A-Za-z0-9_\\.]+)", m =>
            {
                var t = m.Groups[1].Value;
                var k = m.Groups[2].Value;
                if (ctx.Vars.TryGetValue(k, out var v)) return t + v;
                if (ctx.ExternalInfo.TryGetValue(k, out var ev)) return t + ev;
                if (ctx.Nums.TryGetValue(k, out var nv)) return t + nv.ToString();
                return t + $"<{k}>";
            });

            return result;
        }
    }
}