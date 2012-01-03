// Copyright (c) 2011  Pantelis Georgiadis, Mobile Solutions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// Author:
//    Pantelis Georgiadis (PantelisGeorgiadis@Gmail.com)

using System;
using System.Collections.Generic;
using System.Text;

using Dicom.Data;
using Dicom.IO;

namespace Dicom
{
    public class DicomFileFormatEx : DicomFileFormat
    {
        #region Private Members
        private String _filename;        
        #endregion

        #region Public Properties
        public String Filename
        {
            get { return _filename; }
        }
        #endregion

        #region Constructor
        public DicomFileFormatEx()
        {
            _filename = String.Empty;
        }

        public DicomFileFormatEx(String filename)
        {
            _filename = filename;
        }

        public DicomFileFormatEx(DcmDataset dataset) : base(dataset)
        {
            _filename = String.Empty;
        }
        #endregion

        #region Public Methods
        public DicomReadStatus Load(DicomReadOptions options)
        {
            if (!String.IsNullOrEmpty(_filename))
            {
                return Load(_filename, options);
            }
            return DicomReadStatus.UnknownError;
        }
        #endregion
    }
}
