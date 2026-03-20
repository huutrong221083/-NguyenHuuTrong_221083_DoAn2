using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class TaskDelayRegressionInput
    {
        public float DoKho { get; set; }

        public float DoUuTien { get; set; }

        public float SoNguoiThamGia { get; set; }

        public float SoNgayConLaiDenHan { get; set; }

        public float TiLeHoanThanh { get; set; }

        public float TiLeTreHanDuAn { get; set; }

        [ColumnName("Label")]
        public float SoNgayTreThucTe { get; set; }
    }
}
