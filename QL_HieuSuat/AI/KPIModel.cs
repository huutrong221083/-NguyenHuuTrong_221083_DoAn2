using Microsoft.ML.Data;

namespace QL_HieuSuat.AI
{
    public class KPIModel
    {
        public float Sonamkinhnghiem { get; set; }

        //public float DiemkpiTichluy { get; set; }

        public float SoCongViec { get; set; }

        public float DoKhoTrungBinh { get; set; }

        public float DoUuTienTrungBinh { get; set; }

        public float TyLeTreHan { get; set; }

        [ColumnName("Label")]
        public float KPIThucTe { get; set; }
    }
}