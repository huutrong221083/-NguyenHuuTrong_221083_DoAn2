using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class EmployeePerformanceClassificationPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        [ColumnName("Score")]
        public float[] Score { get; set; } = [];
    }
}
