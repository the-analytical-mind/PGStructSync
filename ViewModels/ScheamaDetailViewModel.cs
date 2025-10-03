using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructSync.ViewModels
{
    public class ScheamaDetailViewModel
    {
       


    }


    public class DataBaseDetailInfo
    {
        public string DatabaseName { get; set; }

        public List<ScheamaDetailInfo> Schemas { get; set; }
        public SchemaStatus Status { get; set; }
    }
        

    public class ScheamaDetailInfo
    {
        public string ScheamaName { get; set; }
        public string Query { get; set; }
        public List<TableDetailInfo> Tables { get; set; }
        public List<ViewDetailInfo> Views { get; set; }
        public List<FunctionDetailInfo> Functions { get; set; }
        public List<ProcedureDetailInfo> Procedures { get; set; }
        public SchemaStatus Status { get; set; }
    }
    public class FunctionDetailInfo
    {
        public string FunctionName { get; set; }
        public string Query { get; set; }
        public SchemaStatus Status { get; set; }


    }

    public class ProcedureDetailInfo
    {
        public string ProcedureName { get; set; }
        public string Query { get; set; }
        public SchemaStatus Status { get; set; }
    }

    public class ViewDetailInfo
    {
        public string ViewName { get; set; }
        public string Query { get; set; }
        public SchemaStatus Status { get; set; }
    }


    public class TableDetailInfo
    {
        public string TableName { get; set; }
        public List<TableColumnsInfo> Columns { get; set; }
        public List<TableIndexingInfo> Indexes { get; set; }
        public List<string> ForeignKeys { get; set; }
        public string Query { get; set; }
        public SchemaStatus Status { get; set; }

    }

    public class TableIndexingInfo
    {
        public string IndexName { get; set; }
        public string Query { get; set; }
        public SchemaStatus Status { get; set; }

    }

    public class TableColumnsInfo
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool IsPrimary { get; set; }
        public string Query { get; set; }
        public SchemaStatus Status { get; set; }

    }



    public enum SchemaStatus
    {
        NotExist,
        Matched,
        Modified

    }
}
