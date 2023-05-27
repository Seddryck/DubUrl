using NUnit.Framework;

namespace DubUrl.QA.MsExcel
{
    [Category("MsExcel")]
    public class OleDbProviderMsExcel : BaseOleDbProvider
    {
        public override string ConnectionString
        {
            get => $"oledb+xlsx:///MsExcel/customer.xlsx";
        }

        [Test]
        public override void QueryCustomer()
            => QueryCustomer("select[FullName] from [Customer$] where [CustomerId]=1");

        [Test]
        public override void QueryCustomerWithParams()
            => QueryCustomerWithParams("select [FullName] from [Customer$] where [CustomerId]=?");
    }
}