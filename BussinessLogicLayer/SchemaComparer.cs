using StructSync.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace StructSync.BussinessLogicLayer
{
    public class SchemaComparer
    {
        public async Task CompareSchemas(List<ScheamaDetailInfo> source, List<ScheamaDetailInfo> target)
        {
            if (source == null) return;

            // Build quick lookup for target schemas by name
            var targetSchemas = (target ?? new List<ScheamaDetailInfo>())
                .ToDictionary(s => s.ScheamaName, StringComparer.OrdinalIgnoreCase);

            foreach (var s in source)
            {
                if (!targetSchemas.TryGetValue(s.ScheamaName, out var ts))
                {
                    // Schema missing on target -> everything under it is NotExist
                    s.Status = SchemaStatus.NotExist;
                    MarkAllChildrenStatus(s, SchemaStatus.NotExist);
                    continue;           
                }

                // Schema exists on target; determine child statuses
                // We'll mark schema Matched only if all children are Matched; otherwise Modified
                bool anyChildNotMatched = false;

                // Tables
                if (s.Tables != null)
                {
                    var tgtTables = (ts.Tables ?? new List<TableDetailInfo>())
                        .ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);

                    foreach (var tbl in s.Tables)
                    {
                        if (!tgtTables.TryGetValue(tbl.TableName, out var tgtTbl))
                        {
                            tbl.Status = SchemaStatus.NotExist;
                            MarkTableChildrenStatus(tbl, SchemaStatus.NotExist);
                            anyChildNotMatched = true;
                            continue;
                        }

                        // Table exists -> compare columns, indexes, FKs
                        bool tableModified = false;

                        // Columns
                        if (tbl.Columns != null)
                        {
                            var tgtCols = (tgtTbl.Columns ?? new List<TableColumnsInfo>())
                                .ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

                            foreach (var col in tbl.Columns)
                            {
                                if (!tgtCols.TryGetValue(col.ColumnName, out var tgtCol))
                                {
                                    col.Status = SchemaStatus.NotExist;
                                    tableModified = true;
                                    continue;
                                }

                                // compare datatype (simple normalized string compare)
                                if (!NormalizeSql(col.DataType).Equals(NormalizeSql(tgtCol.DataType), StringComparison.OrdinalIgnoreCase))
                                {
                                    col.Status = SchemaStatus.Modified;
                                    tableModified = true;
                                }
                                else
                                {
                                    col.Status = SchemaStatus.Matched;
                                }
                            }
                        }

                        // Indexes
                        if (tbl.Indexes != null)
                        {
                            var tgtIdx = (tgtTbl.Indexes ?? new List<TableIndexingInfo>())
                                .ToDictionary(i => i.IndexName, StringComparer.OrdinalIgnoreCase);

                            foreach (var idx in tbl.Indexes)
                            {
                                if (!tgtIdx.TryGetValue(idx.IndexName, out var tIdx))
                                {
                                    idx.Status = SchemaStatus.NotExist;
                                    tableModified = true;
                                }
                                else
                                {
                                    if (!NormalizeSql(idx.Query).Equals(NormalizeSql(tIdx.Query), StringComparison.OrdinalIgnoreCase))
                                    {
                                        idx.Status = SchemaStatus.Modified;
                                        tableModified = true;
                                    }
                                    else
                                    {
                                        idx.Status = SchemaStatus.Matched;
                                    }
                                }
                            }
                        }

                        // Foreign keys (raw SQL) - best-effort
                        if (tbl.ForeignKeys != null)
                        {
                            var tgtFks = new HashSet<string>((tgtTbl.ForeignKeys ?? new List<string>())
                                .Select(f => NormalizeSql(f)), StringComparer.OrdinalIgnoreCase);

                            for (int i = 0; i < tbl.ForeignKeys.Count; i++)
                            {
                                var fk = tbl.ForeignKeys[i];
                                if (!tgtFks.Contains(NormalizeSql(fk)))
                                {
                                    // we don't have a FK model to set status on, so mark table
                                    tableModified = true;
                                }
                            }
                        }

                        tbl.Status = tableModified ? SchemaStatus.Modified : SchemaStatus.Matched;
                        if (tbl.Status != SchemaStatus.Matched) anyChildNotMatched = true;
                    }
                }

                // Views
                if (s.Views != null)
                {
                    var tgtViews = (ts.Views ?? new List<ViewDetailInfo>())
                        .ToDictionary(v => v.ViewName, StringComparer.OrdinalIgnoreCase);

                    foreach (var v in s.Views)
                    {
                        if (!tgtViews.TryGetValue(v.ViewName, out var tv))
                        {
                            v.Status = SchemaStatus.NotExist;
                            anyChildNotMatched = true;
                        }
                        else
                        {
                            v.Status = NormalizeSql(v.Query).Equals(NormalizeSql(tv.Query), StringComparison.OrdinalIgnoreCase)
                                ? SchemaStatus.Matched : SchemaStatus.Modified;
                            if (v.Status != SchemaStatus.Matched) anyChildNotMatched = true;
                        }
                    }
                }

                // Functions
                if (s.Functions != null)
                {
                    var tgtFns = (ts.Functions ?? new List<FunctionDetailInfo>())
                        .ToDictionary(f => f.FunctionName, StringComparer.OrdinalIgnoreCase);

                    foreach (var f in s.Functions)
                    {
                        if (!tgtFns.TryGetValue(f.FunctionName, out var tf))
                        {
                            f.Status = SchemaStatus.NotExist;
                            anyChildNotMatched = true;
                        }
                        else
                        {
                            f.Status = NormalizeSql(f.Query).Equals(NormalizeSql(tf.Query), StringComparison.OrdinalIgnoreCase)
                                ? SchemaStatus.Matched : SchemaStatus.Modified;
                            if (f.Status != SchemaStatus.Matched) anyChildNotMatched = true;
                        }
                    }
                }

                // Procedures
                if (s.Procedures != null)
                {
                    var tgtProcs = (ts.Procedures ?? new List<ProcedureDetailInfo>())
                        .ToDictionary(p => p.ProcedureName, StringComparer.OrdinalIgnoreCase);

                    foreach (var p in s.Procedures)
                    {
                        if (!tgtProcs.TryGetValue(p.ProcedureName, out var tp))
                        {
                            p.Status = SchemaStatus.NotExist;
                            anyChildNotMatched = true;
                        }
                        else
                        {
                            p.Status = NormalizeSql(p.Query).Equals(NormalizeSql(tp.Query), StringComparison.OrdinalIgnoreCase)
                                ? SchemaStatus.Matched : SchemaStatus.Modified;
                            if (p.Status != SchemaStatus.Matched) anyChildNotMatched = true;
                        }
                    }
                }

                // finalize schema status: if any child is not matched -> Modified, else Matched
                s.Status = anyChildNotMatched ? SchemaStatus.Modified : SchemaStatus.Matched;
            }
        }

        private static void MarkAllChildrenStatus(ScheamaDetailInfo schema, SchemaStatus status)
        {
            if (schema.Tables != null)
            {
                foreach (var t in schema.Tables)
                {
                    t.Status = status;
                    MarkTableChildrenStatus(t, status);
                }
            }
            if (schema.Views != null)
            {
                foreach (var v in schema.Views) v.Status = status;
            }
            if (schema.Functions != null)
            {
                foreach (var f in schema.Functions) f.Status = status;
            }
            if (schema.Procedures != null)
            {
                foreach (var p in schema.Procedures) p.Status = status;
            }
        }

        private static void MarkTableChildrenStatus(TableDetailInfo table, SchemaStatus status)
        {
            if (table.Columns != null)
            {
                foreach (var c in table.Columns) c.Status = status;
            }
            if (table.Indexes != null)
            {
                foreach (var i in table.Indexes) i.Status = status;
            }
            // foreign keys are strings; nothing to set per-FK
        }

        private static SchemaStatus PromoteStatus(SchemaStatus current, SchemaStatus child)
        {
            // Priority: Modified > NotExist > Matched
            if (current == SchemaStatus.Modified || child == SchemaStatus.Modified) return SchemaStatus.Modified;
            if (current == SchemaStatus.NotExist || child == SchemaStatus.NotExist) return SchemaStatus.NotExist;
            return SchemaStatus.Matched;
        }

        private static string NormalizeSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return string.Empty;
            var parts = sql.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var joined = string.Join(' ', parts).Trim();
            if (joined.EndsWith(";")) joined = joined.Substring(0, joined.Length - 1).TrimEnd();
            return joined.ToLowerInvariant();
        }
    }
}
