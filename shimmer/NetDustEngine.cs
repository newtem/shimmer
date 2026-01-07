using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NetDust
{
    public class NetDustContext
    {
        public Dictionary<string, string> Vars { get; } = new Dictionary<string, string>();
        public Dictionary<string, double> Nums { get; } = new Dictionary<string, double>();
        public List<string> Log { get; } = new List<string>();

        public void AddLog(string msg)
        {
            Log.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
        }
    }

    public class NetDustEngine
    {
        public NetDustContext ExecuteScript(string script)
        {
            NetDustContext ctx = new NetDustContext();
            string[] lines = script.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string raw in lines)
            {
                string cmd = raw.Trim();

                if (string.IsNullOrWhiteSpace(cmd))
                    continue;

                // Net Dust 2.2 Comment: #
                // 2.3 Comment: //
                if (cmd.StartsWith("#") || cmd.StartsWith("//"))
                    continue;

                if (cmd.Equals("code start", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.AddLog("code starts here!");
                }
                else if (cmd.StartsWith("var "))
                {
                    string[] parts = cmd.Substring(4).Split(new char[] { ' ', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        string name = parts[0];
                        ctx.Vars[name] = "";
                        ctx.AddLog("Declared var: " + name);
                    }
                }
                else if (cmd.StartsWith("bring :", StringComparison.OrdinalIgnoreCase))
                {
                    string libName = cmd.Substring(7).Trim(); // bring : " LibName "
                    ctx.AddLog("BRING library recognized: " + libName);
                }
                // num
                else if (cmd.StartsWith("num "))
                {
                    string[] parts = cmd.Substring(4).Split(new char[] { ' ', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        string name = parts[0];
                        ctx.Nums[name] = 0;
                        ctx.AddLog("Declared num: " + name);
                    }
                }

                // get
                else if (cmd.StartsWith("get"))
                {
                    ctx.AddLog("Executed get command → " + cmd);
                }

                // print
                else if (cmd.StartsWith("print"))
                {
                    Match match = Regex.Match(cmd, @"print\((.*?)\)");
                    if (match.Success)
                    {
                        string msg = match.Groups[1].Value;
                        ctx.AddLog("PRINT → " + msg);
                    }
                    else
                    {
                        ctx.AddLog("PRINT (raw): " + cmd);
                    }
                }

                // recognize
                else if (cmd.StartsWith("recognize"))
                {
                    ctx.AddLog("Recognize command executed → " + cmd);
                }

                // IDK
                else
                {
                    ctx.AddLog("Unknown or skipped: " + cmd);
                }
            }

            return ctx;
        }
    }
}
