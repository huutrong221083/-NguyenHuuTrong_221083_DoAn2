using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class ProjectAIPrediction
    {
        [ColumnName("PredictedLabel")]

        public bool PredictedLabel { get; set; }

        [ColumnName("Probability")]

        public float Probability { get; set; }

        [ColumnName("Score")]

        public float Score { get; set; }
    }
}