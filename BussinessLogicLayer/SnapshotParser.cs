
using StructSync.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Parses a pg_dump -s snapshot file into your models:
/// ScheamaDetailInfo, TableDetailInfo, TableColumnsInfo, TableIndexingInfo, ViewDetailInfo,
/// FunctionDetailInfo, ProcedureDetailInfo. Each object's Query property is the full SQL statement.
/// </summary>
/// 


namespace StructSync.BussinessLogicLayer
{


    public class SnapshotParser
    {
        public async Task<List<ScheamaDetailInfo>> ParseSnapshot(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var statements = CollectStatements(lines);
            return BuildModelsFromStatements(statements);
        }

        // Represents a captured SQL statement with its full text and type hint
        private class SqlStmt
        {
            public string Type;      // e.g. TABLE, FUNCTION, VIEW, ALTER, INDEX, SCHEMA, PROCEDURE, OTHER
            public string FullText;
        }

        private List<SqlStmt> CollectStatements(string[] lines)
        {
            var stmts = new List<SqlStmt>();
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];
                if (Regex.IsMatch(line, @"^\s*CREATE\s+FUNCTION\b", RegexOptions.IgnoreCase))
                {
                    var (full, nextIndex) = CollectFunctionOrProcedure(lines, i);
                    stmts.Add(new SqlStmt { Type = "FUNCTION", FullText = full });
                    i = nextIndex + 1;
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*CREATE\s+PROCEDURE\b", RegexOptions.IgnoreCase))
                {
                    var (full, nextIndex) = CollectFunctionOrProcedure(lines, i); // same logic as function
                    stmts.Add(new SqlStmt { Type = "PROCEDURE", FullText = full });
                    i = nextIndex + 1;
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*CREATE\s+TABLE\b", RegexOptions.IgnoreCase))
                {
                    var (full, nextIndex) = CollectUntilClosingParenthesis(lines, i);
                    stmts.Add(new SqlStmt { Type = "TABLE", FullText = full });
                    i = nextIndex + 1;
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*CREATE\s+VIEW\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"^\s*CREATE\s+INDEX\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"^\s*CREATE\s+SCHEMA\b", RegexOptions.IgnoreCase))
                {
                    var (full, nextIndex) = CollectUntilSemicolon(lines, i);
                    var t = Regex.Match(line, @"CREATE\s+(\w+)", RegexOptions.IgnoreCase).Groups[1].Value.ToUpper();
                    stmts.Add(new SqlStmt { Type = t, FullText = full });
                    i = nextIndex + 1;
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*ALTER\s+TABLE\b", RegexOptions.IgnoreCase))
                {
                    // constraints and ALTER TABLE statements - collect until semicolon
                    var (full, nextIndex) = CollectUntilSemicolon(lines, i);
                    stmts.Add(new SqlStmt { Type = "ALTER", FullText = full });
                    i = nextIndex + 1;
                    continue;
                }

                // otherwise skip or collect general statement if it ends with ';'
                if (line.TrimEnd().EndsWith(";"))
                {
                    stmts.Add(new SqlStmt { Type = "OTHER", FullText = line });
                    i++;
                    continue;
                }

                // not starting a statement we care about - move forward
                i++;
            }

            return stmts;
        }

        private (string full, int lastIndex) CollectUntilSemicolon(string[] lines, int startIndex)
        {
            var buffer = new List<string>();
            int j = startIndex;
            for (; j < lines.Length; j++)
            {
                buffer.Add(lines[j]);
                if (lines[j].TrimEnd().EndsWith(";"))
                    break;
            }
            return (string.Join(Environment.NewLine, buffer), Math.Min(j, lines.Length - 1));
        }

        private (string full, int lastIndex) CollectUntilClosingParenthesis(string[] lines, int startIndex)
        {
            // For CREATE TABLE: find the line that ends with ");"
            var buffer = new List<string>();
            int j = startIndex;
            for (; j < lines.Length; j++)
            {
                buffer.Add(lines[j]);
                if (lines[j].TrimEnd().EndsWith(");"))
                    break;
            }

            // fallback: collect until first semicolon if ");" not found
            if (j >= lines.Length || !lines[j].TrimEnd().EndsWith(");"))
            {
                for (; j < lines.Length; j++)
                {
                    if (lines[j].TrimEnd().EndsWith(";"))
                    {
                        buffer.Add(lines[j]);
                        break;
                    }
                }
            }

            return (string.Join(Environment.NewLine, buffer), Math.Min(j, lines.Length - 1));
        }

        private (string full, int lastIndex) CollectFunctionOrProcedure(string[] lines, int startIndex)
        {
            // Need to detect dollar-quoting (AS $tag$) and read until the matching $tag$ and trailing semicolon.
            var buffer = new List<string>();
            int j = startIndex;
            int asLine = -1;
            string tag = null;

            // First scan forward to find the "AS $tag$" marker (could be on same line or later)
            for (int k = startIndex; k < lines.Length; k++)
            {
                buffer.Add(lines[k]);
                var m = Regex.Match(lines[k], @"AS\s+\$([A-Za-z0-9_]*)\$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    tag = m.Groups[1].Value; // may be empty string if $$ used
                    asLine = k;
                    j = k + 1;
                    break;
                }
                // if we reach a semicolon before finding AS $..$, probably a short statement -> fallback
                if (lines[k].TrimEnd().EndsWith(";"))
                {
                    return (string.Join(Environment.NewLine, buffer), k);
                }
            }

            if (asLine == -1)
            {
                // fallback - no dollar quote found; collect until semicolon
                return CollectUntilSemicolon(lines, startIndex);
            }

            string delim = "$" + tag + "$";
            // continue scanning until we find closing delimiter and trailing semicolon (or semicolon later)
            for (int k = j; k < lines.Length; k++)
            {
                buffer.Add(lines[k]);
                if (lines[k].Contains(delim))
                {
                    // now from here look for the semicolon (typically same line ends with ";")
                    if (lines[k].TrimEnd().EndsWith(";"))
                    {
                        return (string.Join(Environment.NewLine, buffer), k);
                    }
                    // else find next line that ends with ;
                    for (int m2 = k; m2 < lines.Length; m2++)
                    {
                        buffer.Add(lines[m2]);
                        if (lines[m2].TrimEnd().EndsWith(";"))
                            return (string.Join(Environment.NewLine, buffer), m2);
                    }
                    // if none found, return what we have
                    return (string.Join(Environment.NewLine, buffer), Math.Min(k, lines.Length - 1));
                }
            }

            // if we reached EOF without closing delim, fallback: collect until first semicolon from startIndex
            return CollectUntilSemicolon(lines, startIndex);
        }

        private List<ScheamaDetailInfo> BuildModelsFromStatements(List<SqlStmt> statements)
        {
            var schemas = new Dictionary<string, ScheamaDetailInfo>(StringComparer.OrdinalIgnoreCase);

            // First pass: create schema objects and table/view/function/proc objects with their full query
            foreach (var s in statements)
            {
                if (s.Type.Equals("SCHEMA", StringComparison.OrdinalIgnoreCase))
                {
                    var m = Regex.Match(s.FullText, @"CREATE\s+SCHEMA\s+(""(?<q>[^""]+)""|(?<u>\w+))", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var name = m.Groups["q"].Success ? m.Groups["q"].Value : m.Groups["u"].Value;
                        EnsureSchema(schemas, name).Query = s.FullText;
                    }
                    continue;
                }

                if (s.Type.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    var nameMatch = Regex.Match(s.FullText, @"CREATE\s+TABLE\s+(?<schema>\w+)\.(?:(?:\""(?<tq>[^""]+)\"")|(?<tu>\w+))", RegexOptions.IgnoreCase);
                    if (!nameMatch.Success) continue;

                    var schemaName = nameMatch.Groups["schema"].Value;
                    var tableName = nameMatch.Groups["tq"].Success ? nameMatch.Groups["tq"].Value : nameMatch.Groups["tu"].Value;

                    var table = new TableDetailInfo
                    {
                        TableName = tableName,
                        Columns = new List<TableColumnsInfo>(),
                        Indexes = new List<TableIndexingInfo>(),
                        ForeignKeys = new List<string>(),
                        Query = s.FullText.Trim(),
                        Status = SchemaStatus.Matched
                    };

                    // parse columns inside CREATE TABLE block
                    var colMatches = Regex.Matches(s.FullText, @"^\s*""(?<col>[^""]+)""\s+(?<type>[^\n,^\r]+)", RegexOptions.Multiline);
                    foreach (Match cm in colMatches)
                    {
                        var colName = cm.Groups["col"].Value;
                        var colDef = cm.Groups["type"].Value.Trim();
                        // Remove trailing comma if present in the original line
                        // Build an ADD COLUMN query for this column (use quoted table name if needed)
                        string quotedTable = NeedsQuoting(tableName) ? $"\"{tableName}\"" : tableName;
                        string quotedSchema = NeedsQuoting(schemaName) ? $"\"{schemaName}\"" : schemaName;
                        string colDefLine = cm.Value.Trim().TrimEnd(','); // original column-def text
                        string addColumnQuery = $"ALTER TABLE {quotedSchema}.{quotedTable} ADD COLUMN {colDefLine};";

                        table.Columns.Add(new TableColumnsInfo
                        {
                            ColumnName = colName,
                            DataType = colDef,
                            IsPrimary = false,
                            Query = addColumnQuery,
                            Status = SchemaStatus.Matched
                        });
                    }

                    EnsureSchema(schemas, schemaName).Tables.Add(table);
                    continue;
                }

                if (s.Type.Equals("VIEW", StringComparison.OrdinalIgnoreCase))
                {
                    var nameMatch = Regex.Match(s.FullText, @"CREATE\s+VIEW\s+(?<schema>\w+)\.(?:(?:\""(?<vq>[^""]+)\"")|(?<vu>\w+))", RegexOptions.IgnoreCase);
                    if (!nameMatch.Success) continue;

                    var schemaName = nameMatch.Groups["schema"].Value;
                    var viewName = nameMatch.Groups["vq"].Success ? nameMatch.Groups["vq"].Value : nameMatch.Groups["vu"].Value;

                    EnsureSchema(schemas, schemaName).Views.Add(new ViewDetailInfo
                    {
                        ViewName = viewName,
                        Query = s.FullText.Trim(),
                        Status = SchemaStatus.Matched
                    });
                    continue;
                }

                if (s.Type.Equals("FUNCTION", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract schema and function name (strip params)
                    var m = Regex.Match(s.FullText, @"CREATE\s+FUNCTION\s+(?<schema>\w+)\.(?<rest>[^\s(]+)", RegexOptions.IgnoreCase);
                    if (!m.Success) continue;
                    var schemaName = m.Groups["schema"].Value;
                    var fullname = m.Groups["rest"].Value;
                    // rest may include parentheses, remove them
                    var fnName = fullname.Split('(')[0].Trim('"');

                    EnsureSchema(schemas, schemaName).Functions.Add(new FunctionDetailInfo
                    {
                        FunctionName = fnName,
                        Query = s.FullText.Trim(),
                        Status = SchemaStatus.Matched
                    });
                    continue;
                }

                if (s.Type.Equals("PROCEDURE", StringComparison.OrdinalIgnoreCase))
                {
                    var m = Regex.Match(s.FullText, @"CREATE\s+PROCEDURE\s+(?<schema>\w+)\.(?<rest>[^\s(]+)", RegexOptions.IgnoreCase);
                    if (!m.Success) continue;
                    var schemaName = m.Groups["schema"].Value;
                    var procName = m.Groups["rest"].Value.Split('(')[0].Trim('"');

                    EnsureSchema(schemas, schemaName).Procedures.Add(new ProcedureDetailInfo
                    {
                        ProcedureName = procName,
                        Query = s.FullText.Trim(),
                        Status = SchemaStatus.Matched
                    });
                    continue;
                }

                if (s.Type.Equals("INDEX", StringComparison.OrdinalIgnoreCase))
                {
                    // try to attach to the last table parsed for that schema if possible
                    var m = Regex.Match(s.FullText, @"CREATE\s+INDEX\s+(?<iname>\w+)\s+ON\s+(?<schema>\w+)\.(?:(?:\""(?<tq>[^""]+)\"")|(?<tu>\w+))", RegexOptions.IgnoreCase);
                    if (!m.Success) continue;
                    var schemaName = m.Groups["schema"].Value;
                    var tableName = m.Groups["tq"].Success ? m.Groups["tq"].Value : m.Groups["tu"].Value;
                    var idxName = m.Groups["iname"].Value;

                    var schemaObj = EnsureSchema(schemas, schemaName);
                    var tableObj = schemaObj.Tables.LastOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                    if (tableObj != null)
                    {
                        tableObj.Indexes.Add(new TableIndexingInfo
                        {
                            IndexName = idxName,
                            Query = s.FullText.Trim(),
                            Status = SchemaStatus.Matched
                        });
                    }
                    continue;
                }

                if (s.Type.Equals("ALTER", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep ALTER statements for second pass (constraints, PK, FK)
                    // We'll process them below (by scanning ALTER statements list)
                    // For now store them in a temporary list attached to a special key
                }
            } // end first pass

            // SECOND PASS: Process ALTER statements to capture PKs/FKs/sequence identity declarations and attach them
            var alterStmts = statements.Where(x => x.Type.Equals("ALTER", StringComparison.OrdinalIgnoreCase)).Select(x => x.FullText).ToList();
            foreach (var alter in alterStmts)
            {
                // Primary key in ALTER TABLE ... ADD CONSTRAINT ... PRIMARY KEY (...)
                var pkMatch = Regex.Match(alter, @"ALTER\s+TABLE\s+(?:ONLY\s+)?(?<schema>\w+)\.(?:(?:\""(?<tq>[^""]+)\"")|(?<tu>\w+)).*ADD\s+CONSTRAINT.*PRIMARY\s+KEY\s*\((?<cols>[^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (pkMatch.Success)
                {
                    var schemaName = pkMatch.Groups["schema"].Value;
                    var tableName = pkMatch.Groups["tq"].Success ? pkMatch.Groups["tq"].Value : pkMatch.Groups["tu"].Value;
                    var colsCsv = pkMatch.Groups["cols"].Value;
                    var colNames = colsCsv.Split(',').Select(c => c.Trim().Trim('"')).ToArray();

                    var schemaObj = EnsureSchema(schemas, schemaName);
                    var tableObj = schemaObj.Tables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                    if (tableObj != null)
                    {
                        foreach (var cn in colNames)
                        {
                            var col = tableObj.Columns.FirstOrDefault(c => c.ColumnName.Equals(cn, StringComparison.OrdinalIgnoreCase));
                            if (col != null) col.IsPrimary = true;
                        }
                    }
                    continue;
                }

                // Foreign key declarations
                var fkMatch = Regex.Match(alter, @"ALTER\s+TABLE\s+(?:ONLY\s+)?(?<schema>\w+)\.(?:(?:\""(?<tq>[^""]+)\"")|(?<tu>\w+)).*ADD\s+CONSTRAINT.*FOREIGN\s+KEY\s*\((?<cols>[^)]+)\)\s+REFERENCES\s+(?<ref>[^\s(]+)\((?<refcols>[^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (fkMatch.Success)
                {
                    var schemaName = fkMatch.Groups["schema"].Value;
                    var tableName = fkMatch.Groups["tq"].Success ? fkMatch.Groups["tq"].Value : fkMatch.Groups["tu"].Value;
                    var schemaObj = EnsureSchema(schemas, schemaName);
                    var tableObj = schemaObj.Tables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                    if (tableObj != null)
                    {
                        tableObj.ForeignKeys.Add(alter.Trim());
                    }
                    continue;
                }

                // Identity sequence change (ALTER TABLE ... ALTER COLUMN ... ADD GENERATED ...)
                var genMatch = Regex.Match(alter, @"ALTER\s+TABLE\s+(?<schema>\w+)\.(?:(?:\""(?<tq>[^""]+)\"")|(?<tu>\w+)).*ALTER\s+COLUMN\s+(?:\""(?<col>[^""]+)\""|(?<col2>\w+)).*ADD\s+GENERATED", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (genMatch.Success)
                {
                    var schemaName = genMatch.Groups["schema"].Value;
                    var tableName = genMatch.Groups["tq"].Success ? genMatch.Groups["tq"].Value : genMatch.Groups["tu"].Value;
                    var colName = genMatch.Groups["col"].Success ? genMatch.Groups["col"].Value : genMatch.Groups["col2"].Value;
                    var schemaObj = EnsureSchema(schemas, schemaName);
                    var tableObj = schemaObj.Tables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                    if (tableObj != null)
                    {
                        // store as an index/sequence-like thing (we don't have a Sequence model; put it into Indexes list with special name)
                        tableObj.Indexes.Add(new TableIndexingInfo
                        {
                            IndexName = $"identity_{colName}",
                            Query = alter.Trim(),
                            Status = SchemaStatus.Matched
                        });
                    }
                    continue;
                }
            }

            // Convert dictionary to list and return
            return schemas.Values.ToList();
        }

        private ScheamaDetailInfo EnsureSchema(Dictionary<string, ScheamaDetailInfo> schemas, string schemaName)
        {
            if (!schemas.TryGetValue(schemaName, out var s))
            {
                s = new ScheamaDetailInfo
                {
                    ScheamaName = schemaName,
                    Query = null,
                    Tables = new List<TableDetailInfo>(),
                    Views = new List<ViewDetailInfo>(),
                    Functions = new List<FunctionDetailInfo>(),
                    Procedures = new List<ProcedureDetailInfo>(),
                    Status = SchemaStatus.Matched
                };
                schemas[schemaName] = s;
            }
            return s;
        }

        private bool NeedsQuoting(string identifier)
        {
            // crude heuristic: if it contains non-lowercase letters/digits/underscore, quote it
            return !Regex.IsMatch(identifier, @"^[a-z0-9_]+$");
        }
    }
}

