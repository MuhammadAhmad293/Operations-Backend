using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Transaction;
using Mapster;

namespace Meezan.Services.Mapper
{
    public class TransactionMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Transaction, TransactionDto>();
        }
    }
}
