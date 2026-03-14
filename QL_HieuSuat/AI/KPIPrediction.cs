using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class KPIPrediction
    {
        [ColumnName("Score")]
        public float PredictedKPI { get; set; }
    }
}