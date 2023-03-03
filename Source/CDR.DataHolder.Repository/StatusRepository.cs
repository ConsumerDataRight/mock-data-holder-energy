using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using CDR.DataHolder.Domain.Entities;
using CDR.DataHolder.Domain.Repositories;
using CDR.DataHolder.Repository.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CDR.DataHolder.Repository
{
    public class StatusRepository : IStatusRepository
    {
        private readonly DataHolderDatabaseContext _dataHolderDatabaseContext;
        private readonly IMapper _mapper;

        public StatusRepository(DataHolderDatabaseContext dataHolderDatabaseContext, IMapper mapper)
        {
            this._dataHolderDatabaseContext = dataHolderDatabaseContext;
            this._mapper = mapper;
        }
       
    }
}
