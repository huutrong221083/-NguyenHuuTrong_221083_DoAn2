using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class ProjectAIModel
    {
        public float SoCongViec { get; set; }

        public float TyLeHoanThanh { get; set; }

        public float TyLeTreHan { get; set; }

        public float DoKhoTrungBinh { get; set; }

        public float DoUuTienTrungBinh { get; set; }

        public float SoNhanVien { get; set; }

        [ColumnName("Label")]
        public bool DuAnTre { get; set; }
    }
}