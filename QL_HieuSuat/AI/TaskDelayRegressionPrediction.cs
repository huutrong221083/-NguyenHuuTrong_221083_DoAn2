using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class TaskDelayRegressionPrediction
    {
        [ColumnName("Score")]
        public float SoNgayTreDuDoan { get; set; }
    }
}
