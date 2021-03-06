﻿using AutoMapper;

namespace LMS.Bootstrap.Mapping
{
    public class AutoMapper : Interfaces.IMapper
    {
        private readonly IMapper mapper;

        public AutoMapper()
        {
            var configuration = new MapperConfiguration(config => config.AddProfile<MappingsContainer>());
            mapper = configuration.CreateMapper();
        }

        public TDest Map<TSrc, TDest>(TSrc src)
        {
            return mapper.Map<TSrc, TDest>(src);
        }

        public void Map<TSrc, TDest>(TSrc src, TDest dest)
        {
            mapper.Map(src, dest);
        }
    }
}
