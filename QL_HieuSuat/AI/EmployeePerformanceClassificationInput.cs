using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class EmployeePerformanceClassificationInput
    {
        public float Sonamkinhnghiem { get; set; }

        public float SoCongViec { get; set; }

        public float DoKhoTrungBinh { get; set; }

        public float DoUuTienTrungBinh { get; set; }

        public float TyLeTreHan { get; set; }

        public float TyLeHoanThanh { get; set; }

        public float DiemKpiGanNhat { get; set; }

        [ColumnName("Label")]
        public string XepLoai { get; set; } = string.Empty;
    }
}
