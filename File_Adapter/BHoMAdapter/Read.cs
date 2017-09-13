﻿using BH.oM.Base;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BH.Adapter.FileAdapter
{
    public partial class FileAdapter
    {
        protected override IEnumerable<BHoMObject> Read(Type type = null, string tag = "")
        {
            IEnumerable<BHoMObject> everything = m_Readable ? ReadJson() : ReadBson();

            if (type != null)
                everything = everything.Where(x => type.IsAssignableFrom(x.GetType()));

            if (tag.Length > 0)
                everything = everything.Where(x => x.Tags.Contains(tag));

            return everything;
        }


        private IEnumerable<BHoMObject> ReadJson()
        {
            string[] json = File.ReadAllLines(m_FilePath);
            return json.Select(x => Convert.FromJson(x) as BHoMObject);
        }


        private IEnumerable<BHoMObject> ReadBson()
        {
            FileStream mongoReadStream = File.OpenRead(m_FilePath);
            var reader = new BsonBinaryReader(mongoReadStream);
            List<BsonDocument> readBson = BsonSerializer.Deserialize(reader, typeof(object)) as List<BsonDocument>;
            return readBson.Select(x => BsonSerializer.Deserialize(x, typeof(object)) as BHoMObject);
        }
    }
}
