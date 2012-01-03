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

#region License

// Copyright (c) 2011, ClearCanvas Inc.
// All rights reserved.
// http://www.clearcanvas.ca
//
// This software is licensed under the Open Software License v3.0.
// For the complete license, see http://www.clearcanvas.ca/OSLv3.0

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Dicom.Data;
using Dicom.IO;

namespace Dicom
{
    /// <summary>
    /// This class reads and/or writes a Dicom Directory file.  
    /// </summary>
    /// <example>
    /// using (DicomDirectory dicomDirectory = new DicomDirectory())
    /// {
    ///     dicomDirectory.SourceApplicationEntityTitle = "UNO";
    ///     dicomDirectory.FileSetId = "My File Set Desc";
    ///     dicomDirectory.AddFile("C:\DicomImages\SomeFile.dcm", "DIR001\\IMAGE001.DCM");
    ///     dicomDirectory.AddFile("C:\DicomImages\AnotherFile.dcm", "DIR002\\IMAGE002.DCM");
    ///     dicomDirectory.AddFile("C:\DicomImages\AnotherFile3.dcm", null);
    ///     dicomDirectory.Save("C:\\Temp\\DICOMDIR");
    /// }
    /// </example>
	/// <example>
	/// using (DicomDirectory dicomDirectory = new DicomDirectory())
	/// {
	///     dicomDirectory.Load("C:\\Temp\\DICOMDIR");
	/// 
	///		int patientRecords = 0;
	///		int studyRecords = 0;
	///		int seriesRecords = 0;
	///		int instanceRecords = 0;
	///
	///		// Show a simple traversal, counting the records at each level
	///		foreach (DirectoryRecordSequenceItem patientRecord in reader.RootDirectoryRecordCollection)
	///		{
	///			patientRecords++;
	///			foreach (DirectoryRecordSequenceItem studyRecord in patientRecord.LowerLevelDirectoryRecordCollection)
	///			{
	///				studyRecords++;
	///				foreach (DirectoryRecordSequenceItem seriesRecord in studyRecord.LowerLevelDirectoryRecordCollection)
	///				{
	///					seriesRecords++;
	///					foreach (DirectoryRecordSequenceItem instanceRecord in seriesRecord.LowerLevelDirectoryRecordCollection)
	///					{
	///						instanceRecords++;
	///					}
	///				}
	///			}
	///		}
	/// }
	/// </example>
	public class DicomDirectory : IDisposable
    {
        #region Private Variables
        /// <summary>The directory record sequence item that all the directory record items gets added to.</summary>
        private DcmItemSequence _directoryRecordSequence;

        /// <summary>The Dicom Directory File</summary>
        private DicomFileFormatEx _dicomDirFile;

        /// <summary>File Name to be saved to (Param to Save method)</summary>
        private string _saveFileName;

        /// <summary>Contains the ongoing fileOffset to determine the offset tags for each Item</summary>
        private uint _fileOffset;

        /// <summary>Contains the first directory record of in the root of the DICOMDIR.</summary>
        private DirectoryRecordSequenceItem _rootRecord;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the DicomDirectory class.
        /// </summary>
        /// <remarks>Sets most default values which can be changed via </remarks>
        /// <param name="aeTitle">The AE Title of the Media Reader/Writer accessing the DICOMDIR</param>
        public DicomDirectory(string aeTitle)
        {
            try
            {
                DicomUID SOPInstanceUID = DicomUID.Generate();

                DcmDataset dataset = new DcmDataset(DicomTransferSyntax.ExplicitVRLittleEndian);
                dataset.AddElementWithValue(DicomTags.SOPClassUID, DicomUID.MediaStorageDirectoryStorage);
                dataset.AddElementWithValue(DicomTags.SOPInstanceUID, SOPInstanceUID);

                _dicomDirFile = new DicomFileFormatEx(dataset);
                _dicomDirFile.Dataset.Remove(DicomTags.SOPInstanceUID);
                    
                _dicomDirFile.FileMetaInfo.FileMetaInformationVersion = DcmFileMetaInfo.Version;
                 _dicomDirFile.FileMetaInfo.MediaStorageSOPClassUID = DicomUID.MediaStorageDirectoryStorage;
                 _dicomDirFile.FileMetaInfo.SourceApplicationEntityTitle = aeTitle;
                 _dicomDirFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
                
                _dicomDirFile.Dataset.AddElementWithValue(DicomTags.FilesetID, String.Empty);

                 ImplementationVersionName = Implementation.Version;
                 ImplementationClassUid = Implementation.ClassUID;
 
                 _dicomDirFile.FileMetaInfo.MediaStorageSOPInstanceUID = SOPInstanceUID;

                // Set zero value so we can calculate the file Offset
                _dicomDirFile.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheFirstDirectoryRecordOfTheRootDirectoryEntity, (UInt32)0);
                _dicomDirFile.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheLastDirectoryRecordOfTheRootDirectoryEntity, (UInt32)0);
                _dicomDirFile.Dataset.AddElementWithObjectValue(DicomTags.FilesetConsistencyFlag, (UInt16)0);

                _directoryRecordSequence = new DcmItemSequence(DicomTags.DirectoryRecordSequence);
                _dicomDirFile.Dataset.AddItem(_directoryRecordSequence);
            }
            catch (Exception)
            {
                Debug.Log.Error("Exception initializing DicomDirectory");
                throw;
            }
        }
        #endregion

        #region Public Properties

		/// <summary>
		/// An enumerable collection for traversing the <see cref="DirectoryRecordSequenceItem"/> records in the root of the DICOMDIR.
		/// </summary>
		public DirectoryRecordCollection RootDirectoryRecordCollection
		{
			get
			{
				return new DirectoryRecordCollection(RootDirectoryRecord);
			}
		}

		/// <summary>
		/// Gets the root directory record.  May be set to null if no directory records exist.
		/// </summary>
    	public DirectoryRecordSequenceItem RootDirectoryRecord
    	{
			get { return _rootRecord; }
    	}

        //NOTE: these are mostly wrappers around the DicomFile properties

        /// <summary>
        /// Gets or sets the file set id.
        /// </summary>
        /// <value>The file set id.</value>
        /// <remarks>User or implementation specific Identifier (up to 16 characters), intended to be a short human readable label to easily (but
        /// not necessarily uniquely) identify a specific File-set to
        /// facilitate operator manipulation of the physical media on
        /// which the File-set is stored. </remarks>
        public string FileSetId
        {
            get { return _dicomDirFile.Dataset.GetString(DicomTags.FilesetID, String.Empty); }
            set
            {
                if (value != null && value.Trim().Length > 16)
					throw new ArgumentException("FileSetId can only be a maximum of 16 characters", "value");

                _dicomDirFile.Dataset.AddElementWithValueString(DicomTags.FilesetID, value == null ? "" : value.Trim());
            }
        }

        /// <summary>
        /// The DICOM Application Entity (AE) Title of the AE which wrote this file's 
        /// content (or last updated it).  If used, it allows the tracin of the source 
        /// of errors in the event of media interchange problems.  The policies associated
        /// with AE Titles are the same as those defined in PS 3.8 of the DICOM Standard. 
        /// </summary>
        public string SourceApplicationEntityTitle
        {
            get { return _dicomDirFile.FileMetaInfo.SourceApplicationEntityTitle; }
            set
            {
                _dicomDirFile.FileMetaInfo.SourceApplicationEntityTitle = value;
            }
        }

        /// <summary>
        /// Identifies a version for an Implementation Class UID (002,0012) using up to 
        /// 16 characters of the repertoire.  It follows the same policies as defined in 
        /// PS 3.7 of the DICOM Standard (association negotiation).
        /// </summary>
        public string ImplementationVersionName
        {
            get { return _dicomDirFile.FileMetaInfo.ImplementationVersionName; }
            set
            {
                _dicomDirFile.FileMetaInfo.ImplementationVersionName = value;
            }
        }

        /// <summary>
        /// Uniquely identifies the implementation which wrote this file and its content.  It provides an 
        /// unambiguous identification of the type of implementation which last wrote the file in the 
        /// event of interchagne problems.  It follows the same policies as defined by PS 3.7 of the DICOM Standard
        /// (association negotiation).
        /// </summary>        
        public DicomUID ImplementationClassUid
        {
            get { return _dicomDirFile.FileMetaInfo.ImplementationClassUID; }
            set
            {
                _dicomDirFile.FileMetaInfo.ImplementationClassUID = value;
            }
        }

        /// <summary>
        /// The transfer syntax the file is encoded in.
        /// </summary>
        /// <remarks>
        /// This property returns a TransferSyntax object for the transfer syntax encoded 
        /// in the tag Transfer Syntax UID (0002,0010).
        /// </remarks>
        public DicomTransferSyntax TransferSyntax
        {
            get { return _dicomDirFile.FileMetaInfo.TransferSyntax; }
            set { _dicomDirFile.FileMetaInfo.TransferSyntax = value; }
        }

        /// <summary>
        /// Uniquiely identifies the SOP Instance associated with the Data Set placed in the file and following the File Meta Information.
        /// </summary>
        public DicomUID MediaStorageSopInstanceUid
        {
            get { return _dicomDirFile.FileMetaInfo.MediaStorageSOPInstanceUID; }
            set { _dicomDirFile.FileMetaInfo.MediaStorageSOPInstanceUID = value; }
        }

        /// <summary>
        /// Identifies a version for an Implementation Class UID (002,0012) using up to 
        /// 16 characters of the repertoire.  It follows the same policies as defined in 
        /// PS 3.7 of the DICOM Standard (association negotiation).
        /// </summary>
        public DicomUID PrivateInformationCreatorUid
        {
            get { return _dicomDirFile.FileMetaInfo.PrivateInformationCreatorUID; }
            set { _dicomDirFile.FileMetaInfo.PrivateInformationCreatorUID = value; }
        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Saves the DICOMDIR to the specified file name.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        public void Save(string fileName)
        {
            DicomWriteOptions options = DicomWriteOptions.None;

            if (_rootRecord == null)
                throw new InvalidOperationException("No Dicom Files added, cannot save dicom directory");

            _saveFileName = fileName;

			// Clear so that the calculations work properly on the length.
			// We wouldn't have to do this, if CalculateWriteLength had a Start/Stop tag
            _directoryRecordSequence.SequenceItems.Clear();
			
			//Remove SopClassUid, so it doesn't messup offsets, add it back in later.
			if (_dicomDirFile.Dataset.Contains(DicomTags.SOPClassUID))
                _dicomDirFile.Dataset.Remove(DicomTags.SOPClassUID);

            //Set initial offset of where the directory record sequence tag starts
            // based on the 128 byte preamble, the DICM characters and the tags themselves.
            _fileOffset = 128 + 4 + _dicomDirFile.FileMetaInfo.CalculateWriteLength(_dicomDirFile.FileMetaInfo.TransferSyntax, DicomWriteOptions.WriteFragmentOffsetTable)
                + _dicomDirFile.Dataset.CalculateWriteLength(_dicomDirFile.FileMetaInfo.TransferSyntax, DicomWriteOptions.WriteFragmentOffsetTable);

            //Add the offset for the Directory Record sequence tag itself
            _fileOffset += 4; // element tag
            if (_dicomDirFile.FileMetaInfo.TransferSyntax.IsExplicitVR)
            {
                _fileOffset += 2; // vr
                _fileOffset += 6; // length
            }
            else
            {
                _fileOffset += 4; // length
            }

            // go through the tree of records and add them back into the dataset.
            AddDirectoryRecordsToSequenceItem(_rootRecord);

            // Double check to make sure at least one file was added.
            if (_rootRecord != null)
            {
                // Calculate offsets for each directory record
                CalculateOffsets(_dicomDirFile.FileMetaInfo.TransferSyntax, options);

                // Traverse through the tree and set the offsets.
                SetOffsets(_rootRecord);

                //Set the offsets in the dataset 
                _dicomDirFile.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheFirstDirectoryRecordOfTheRootDirectoryEntity, (UInt32)_rootRecord.Offset);

                DirectoryRecordSequenceItem lastRoot = _rootRecord;
                while (lastRoot.NextDirectoryRecord != null)
                {
                    lastRoot = lastRoot.NextDirectoryRecord;
                }

                _dicomDirFile.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheLastDirectoryRecordOfTheRootDirectoryEntity, (UInt32)lastRoot.Offset);
            }
            else
            {
                _dicomDirFile.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheFirstDirectoryRecordOfTheRootDirectoryEntity, (UInt32)0);
                _dicomDirFile.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheLastDirectoryRecordOfTheRootDirectoryEntity, (UInt32)0);
            }

            // Add this at the end so it does not mess up offset value...
            _dicomDirFile.Dataset.AddElementWithValue(DicomTags.SOPClassUID, DicomUID.MediaStorageDirectoryStorage);

            try
            {
                _dicomDirFile.Save(fileName, DicomWriteOptions.WriteFragmentOffsetTable);
            }
            catch (Exception)
            {
                Debug.Log.Error("Error saving dicom File {0}", fileName);
            	throw;
            }
        }

        private static T Cast<T>(object o)
        {
            return (T)o;
        }

		/// <summary>
		/// Loads the specified DICOMDIR file.
		/// </summary>
		/// <param name="filename">The path to the DICOMDIR file.</param>
		public void Load(string filename)
		{
			try
			{
                if (_dicomDirFile.Load(filename, DicomReadOptions.Default) != DicomReadStatus.Success)
                {
                    throw new Exception(String.Format("Error loading dicom File {0}", filename));
                }
			}
			catch (Exception)
			{
                Debug.Log.Error("Error loading dicom File {0}", filename);
				throw;
			}

			// Create a Dictionary containing the offsets within the DICOMDIR of each directory record and the 
			// corresponding DirectoryREcordSequenceItem objects.
			Dictionary<uint, DirectoryRecordSequenceItem> lookup = new Dictionary<uint, DirectoryRecordSequenceItem>();

            _directoryRecordSequence = _dicomDirFile.Dataset.GetSQ(DicomTags.DirectoryRecordSequence);
            if (_directoryRecordSequence == null)
                throw new Exception(String.Format("No DirectoryRecordSequence found in dicom File {0}", filename));

            foreach (DcmItemSequenceItem sqItem in _directoryRecordSequence.SequenceItems)
			{
                DirectoryRecordSequenceItem dsqItem = new DirectoryRecordSequenceItem();
                dsqItem.Dataset.Merge(sqItem.Dataset);
                dsqItem.Offset = (uint)sqItem.StreamPosition;
                lookup.Add(dsqItem.Offset, dsqItem);
			}

            // Get the root Directory Record.
            // 
            DcmUnsignedLong ul = null;
            ul = _dicomDirFile.Dataset.GetUL(DicomTags.OffsetOfTheFirstDirectoryRecordOfTheRootDirectoryEntity);
            uint offset = ul != null ? ul.GetValue() : 0;
            if (!lookup.TryGetValue(offset, out _rootRecord) && offset != 0)
                throw new DicomDataException("Unable to find root directory record in File");

            // Now traverse through the remainder of the directory records, and match up the offsets with the directory
            // records so we can build up the tree structure.
            foreach (DirectoryRecordSequenceItem sqItem in lookup.Values)
            {
                ul = sqItem.Dataset.GetUL(DicomTags.OffsetOfTheNextDirectoryRecord);
                offset = ul != null ? ul.GetValue() : 0;

                DirectoryRecordSequenceItem foundItem;
                if (lookup.TryGetValue(offset, out foundItem))
                    sqItem.NextDirectoryRecord = foundItem;
                else
                    sqItem.NextDirectoryRecord = null;

                ul = sqItem.Dataset.GetUL(DicomTags.OffsetOfReferencedLowerLevelDirectoryEntity);
                offset = ul != null ? ul.GetValue() : 0;

                sqItem.LowerLevelDirectoryRecord = lookup.TryGetValue(offset, out foundItem) ? foundItem : null;
            }		
		}

        /// <summary>
        /// Adds the dicom image file to the list of images to add.
        /// </summary>
        /// <param name="dicomFile">The dicom file.</param>
        /// <param name="optionalDicomDirFileLocation">specifies the file location in the Directory Record ReferencedFileId 
        /// tag.  If is null or empty, it will use a relative path to the dicom File from the specified DICOM Dir filename in the Save() method.</param>
        public void AddFile(DicomFileFormatEx dicomFile, string optionalDicomDirFileLocation)
        {
            if (dicomFile == null)
                throw new ArgumentNullException("dicomFile");

			InsertFile(dicomFile, optionalDicomDirFileLocation);
        }

        /// <summary>
        /// Adds the dicom image file to the list of images to add.
        /// </summary>
        /// <param name="dicomFileName">Name of the dicom file.</param>
        /// <param name="optionalDicomDirFileLocation">specifies the file location in the Directory Record ReferencedFileId 
        /// tag.  If is null or empty, it will use a relative path to the dicom File from the specified DICOM Dir filename in the Save() method.</param>
        public void AddFile(string dicomFileName, string optionalDicomDirFileLocation)
        {
            if (String.IsNullOrEmpty(dicomFileName))
                throw new ArgumentNullException("dicomFileName");

			if (File.Exists(dicomFileName))
			{
                DicomFileFormatEx dicomFile = new DicomFileFormatEx(dicomFileName);
				InsertFile(dicomFile, optionalDicomDirFileLocation);
			}
			else
				throw new FileNotFoundException("cannot add DicomFile, does not exist: " + dicomFileName);
        }

        /// <summary>
        /// Dumps the contents of the dicomDirFile.
        /// </summary>
        /// <param name="prefix">The prefix.</param>
        /// <param name="options">The dump options.</param>
        /// <returns></returns>
        public string Dump(string prefix, DicomDumpOptions options)
        {
            StringBuilder sb = new StringBuilder();
            _dicomDirFile.Dataset.Dump(sb, prefix, options);
            return sb.ToString();
        }

        #endregion

        #region Private Methods
		/// <summary>
		/// Called to insert a DICOM file into the directory record structure.
		/// </summary>
		/// <param name="dicomFile"></param>
		/// <param name="optionalRelativeRootPath"></param>
        private void InsertFile(DicomFileFormatEx dicomFile, string optionalRelativeRootPath)
		{
			try
			{
				dicomFile.Load(DicomReadOptions.Default);

				DirectoryRecordSequenceItem patientRecord;
				DirectoryRecordSequenceItem studyRecord;
				DirectoryRecordSequenceItem seriesRecord;

                if (_rootRecord == null)
                    _rootRecord = patientRecord = CreatePatientItem(dicomFile.Dataset);
                else
                    patientRecord = GetExistingOrCreateNewPatient(_rootRecord, dicomFile.Dataset);

				if (patientRecord.LowerLevelDirectoryRecord == null)
					patientRecord.LowerLevelDirectoryRecord = studyRecord = CreateStudyItem(dicomFile.Dataset);
				else
					studyRecord = GetExistingOrCreateNewStudy(patientRecord.LowerLevelDirectoryRecord, dicomFile.Dataset);

				if (studyRecord.LowerLevelDirectoryRecord == null)
					studyRecord.LowerLevelDirectoryRecord = seriesRecord = CreateSeriesItem(dicomFile.Dataset);
				else
					seriesRecord = GetExistingOrCreateNewSeries(studyRecord.LowerLevelDirectoryRecord, dicomFile.Dataset);

				if (seriesRecord.LowerLevelDirectoryRecord == null)
					seriesRecord.LowerLevelDirectoryRecord = CreateImageItem(dicomFile, optionalRelativeRootPath);
				else
					GetExistingOrCreateNewImage(seriesRecord.LowerLevelDirectoryRecord, dicomFile, optionalRelativeRootPath);
			}
			catch (Exception)
			{
                Debug.Log.Error("Error adding image {0} to directory file", dicomFile.Filename);
				throw;
			}
		}

    	/// <summary>
        /// Traverse the directory record tree and insert them into the directory record sequence.
        /// </summary>
        private void AddDirectoryRecordsToSequenceItem(DirectoryRecordSequenceItem root)
        {
            if (root == null)
                return;

            _directoryRecordSequence.AddSequenceItem(root);

            if (root.LowerLevelDirectoryRecord != null)
                AddDirectoryRecordsToSequenceItem(root.LowerLevelDirectoryRecord);

            if (root.NextDirectoryRecord != null)
                AddDirectoryRecordsToSequenceItem(root.NextDirectoryRecord);
        }

        /// <summary>
        /// Finds the next directory record of the specified <paramref name="recordType"/>, starting at the specified <paramref name="startIndex"/>
        /// </summary>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="startIndex">The start index.</param>
        /// <returns></returns>
        private void CalculateOffsets(DicomTransferSyntax syntax, DicomWriteOptions options)
        {
            foreach (DcmItemSequenceItem sq in _dicomDirFile.Dataset.GetSQ(DicomTags.DirectoryRecordSequence).SequenceItems)
            {
                DirectoryRecordSequenceItem record = sq as DirectoryRecordSequenceItem;
                if (record == null)
                    throw new Exception("Unexpected type for directory record: " + sq.GetType());

                record.Offset = _fileOffset;

                _fileOffset += 4 + 4; // Sequence Item Tag

                _fileOffset += record.Dataset.CalculateWriteLength(syntax, options & ~DicomWriteOptions.CalculateGroupLengths);
                if (!Flags.IsSet(options, DicomWriteOptions.ExplicitLengthSequenceItem))
                    _fileOffset += 4 + 4; // Sequence Item Delimitation Item
            }
            if (!Flags.IsSet(options, DicomWriteOptions.ExplicitLengthSequence))
                _fileOffset += 4 + 4; // Sequence Delimitation Item
        }

        /// <summary>
        /// Traverse at the image level to see if the image exists or create a new image if it doesn't.
        /// </summary>
        /// <param name="images"></param>
        /// <param name="file"></param>
        /// <param name="optionalDicomDirFileLocation"></param>
        /// <returns></returns>
        private void GetExistingOrCreateNewImage(DirectoryRecordSequenceItem images, DicomFileFormatEx file, string optionalDicomDirFileLocation)
        {
            DirectoryRecordSequenceItem currentImage = images;
            while (currentImage != null)
            {
                if (currentImage.Dataset.GetElement(DicomTags.ReferencedSOPInstanceUIDInFile).Equals(file.Dataset.GetElement(DicomTags.SOPInstanceUID)))
                {
                	return;
                }
            	if (currentImage.NextDirectoryRecord == null)
                {
                    currentImage.NextDirectoryRecord = CreateImageItem(file, optionalDicomDirFileLocation);
                	return;
                }
                currentImage = currentImage.NextDirectoryRecord;
            }
        	return;
        }

        /// <summary>
        /// Create an image Directory record
        /// </summary>
        /// <param name="dicomFile">The dicom file.</param>
        /// <param name="optionalDicomDirFileLocation">The optional dicom dir file location.</param>
        private DirectoryRecordSequenceItem CreateImageItem(DicomFileFormatEx dicomFile, string optionalDicomDirFileLocation)
        {
            if (String.IsNullOrEmpty(optionalDicomDirFileLocation))
            {
                optionalDicomDirFileLocation = EvaluateRelativePath(_saveFileName, dicomFile.Filename);
            }

        	DirectoryRecordType type;
			if (DirectoryRecordDictionary.TryGetDirectoryRecordType(dicomFile.FileMetaInfo.MediaStorageSOPClassUID.UID, out type))
			{
				string name;
				DirectoryRecordTypeDictionary.TryGetName(type, out name);

                IDictionary<DicomTag, object> dicomTags = new Dictionary<DicomTag, object>();
				dicomTags.Add(DicomTags.ReferencedFileID, optionalDicomDirFileLocation);
                dicomTags.Add(DicomTags.ReferencedSOPClassUIDInFile, dicomFile.FileMetaInfo.MediaStorageSOPClassUID.UID);
				dicomTags.Add(DicomTags.ReferencedSOPInstanceUIDInFile, dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID.UID);
                dicomTags.Add(DicomTags.ReferencedTransferSyntaxUIDInFile, dicomFile.FileMetaInfo.TransferSyntax.UID.UID);

				// NOTE:  This is a bit problematic, but sufficient for now. We should take into account
				// which tags are type 2 and which are type 1 and which are conditional when setting them 
				// in AddSequenceItem
                List<DicomTag> tagList;
				if (DirectoryRecordDictionary.TryGetDirectoryRecordTagList(type, out tagList))
                    foreach (DicomTag tag in tagList)
						dicomTags.Add(tag, null);

                // FIX: Some viewers use this tag to sort images (e.g. Siemens Syngo)
                dicomTags.Add(DicomTags.InstanceNumber, null);

				return AddSequenceItem(type, dicomFile.Dataset, dicomTags);
			}

        	return null;
        }
        #endregion

        #region Private Static Methods
        /// <summary>
        /// Traverse through the tree of directory records, and set the values for the offsets for each 
        /// record.
        /// </summary>
        private static void SetOffsets(DirectoryRecordSequenceItem root)
        {
            if (root.NextDirectoryRecord != null)
            {
                root.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheNextDirectoryRecord, (UInt32)root.NextDirectoryRecord.Offset);
                SetOffsets(root.NextDirectoryRecord);
            }
            else
                root.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheNextDirectoryRecord, (UInt32)0);

            if (root.LowerLevelDirectoryRecord != null)
            {
                root.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfReferencedLowerLevelDirectoryEntity, (UInt32)root.LowerLevelDirectoryRecord.Offset);
                SetOffsets(root.LowerLevelDirectoryRecord);
            }
            else
                root.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfReferencedLowerLevelDirectoryEntity, (UInt32)0);
        }

        /// <summary>
        /// Traverse at the Patient level to check if a Patient exists or create a Patient if it doesn't exist.
        /// </summary>
        /// <param name="patients"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        private static DirectoryRecordSequenceItem GetExistingOrCreateNewPatient(DirectoryRecordSequenceItem patients, DcmDataset dataset)
        {
            DirectoryRecordSequenceItem currentPatient = patients;
            while (currentPatient != null)
            {
                if (currentPatient.Dataset.GetValueString(DicomTags.PatientID) == dataset.GetValueString(DicomTags.PatientID)
                    && currentPatient.Dataset.GetValueString(DicomTags.PatientsName) == dataset.GetValueString(DicomTags.PatientsName))
                {
                    return currentPatient;
                }
                if (currentPatient.NextDirectoryRecord == null)
                {
                    currentPatient.NextDirectoryRecord = CreatePatientItem(dataset);
                    return currentPatient.NextDirectoryRecord;
                }
                currentPatient = currentPatient.NextDirectoryRecord;
            }
            return null;
        }

        /// <summary>
        /// Traverse at the Study level to check if a Study exists or create a Study if it doesn't exist.
        /// </summary>
        /// <param name="studies"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        private static DirectoryRecordSequenceItem GetExistingOrCreateNewStudy(DirectoryRecordSequenceItem studies, DcmDataset dataset)
        {
            DirectoryRecordSequenceItem currentStudy = studies;
            while (currentStudy != null)
            {
                if (currentStudy.Dataset.GetValueString(DicomTags.StudyInstanceUID) == dataset.GetValueString(DicomTags.StudyInstanceUID))
                {
                    return currentStudy;
                }
                if (currentStudy.NextDirectoryRecord == null)
                {
                    currentStudy.NextDirectoryRecord = CreateStudyItem(dataset);
                    return currentStudy.NextDirectoryRecord;
                }
                currentStudy = currentStudy.NextDirectoryRecord;
            }
            return null;
        }

        /// <summary>
        /// Traverse at the Series level to check if a Series exists, or create a Series if it doesn't exist.
        /// </summary>
        /// <param name="series"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        private static DirectoryRecordSequenceItem GetExistingOrCreateNewSeries(DirectoryRecordSequenceItem series, DcmDataset dataset)
        {
            DirectoryRecordSequenceItem currentSeries = series;
            while (currentSeries != null)
            {
                if (currentSeries.Dataset.GetValueString(DicomTags.SeriesInstanceUID) == dataset.GetValueString(DicomTags.SeriesInstanceUID))
                {
                    return currentSeries;
                }
                if (currentSeries.NextDirectoryRecord == null)
                {
                    currentSeries.NextDirectoryRecord = CreateSeriesItem(dataset);
                    return currentSeries.NextDirectoryRecord;
                }
                currentSeries = currentSeries.NextDirectoryRecord;
            }
            return null;
        }

        /// <summary>
        /// Adds a sequence item to temporarydictionary with the current offset.
        /// </summary>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="dataSet">The data set.</param>
        /// <param name="tags">The tags.</param>
        /// <returns>The newly created DirectoryRecord</returns>
        /// <remarks>Tags are a dictionary of tags and optional values - if the value is null, then it will get the value from the specified dataset</remarks>
        private static DirectoryRecordSequenceItem AddSequenceItem(DirectoryRecordType recordType, DcmDataset dataset, IDictionary<DicomTag, object> tags)
        {
            DirectoryRecordSequenceItem dicomSequenceItem = new DirectoryRecordSequenceItem();

            dicomSequenceItem.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfTheNextDirectoryRecord, (UInt32)0);
            dicomSequenceItem.Dataset.AddElementWithObjectValue(DicomTags.RecordInuseFlag, (UInt16)0xFFFF);
            dicomSequenceItem.Dataset.AddElementWithObjectValue(DicomTags.OffsetOfReferencedLowerLevelDirectoryEntity, (UInt32)0);

        	string recordName;
        	DirectoryRecordTypeDictionary.TryGetName(recordType, out recordName);
            dicomSequenceItem.Dataset.AddElementWithValueString(DicomTags.DirectoryRecordType, recordName);

        	DcmElement charSetElement;
            charSetElement = dataset.GetElement(DicomTags.SpecificCharacterSet);
            if (charSetElement != null)
                dicomSequenceItem.Dataset.AddElementWithObjectValue(DicomTags.SpecificCharacterSet, charSetElement.GetValueObject());


            foreach (DicomTag dicomTag in tags.Keys)
            {
                try
                {
                    DcmElement element;
                    element = dataset.GetElement(dicomTag);

                    if (tags[dicomTag] != null)
                    {
                        dicomSequenceItem.Dataset.AddElementWithObjectValue(dicomTag, tags[dicomTag]); 
                    }
                    else if (element != null)
                    {
                        dicomSequenceItem.Dataset.AddElementWithObjectValue(dicomTag, element.GetValueObject());
                    }
                    else
                    {
                        Debug.Log.Info("Cannot find dicomTag {0} for record type {1}",
                            dicomTag != null ? dicomTag.ToString() : dicomTag.ToString(), recordType);
                    }
                }
                catch (Exception)
                {
                    Debug.Log.Error("Exception adding dicomTag {0} to directory record for record type {1}", dicomTag, recordType);
                }
            }

            return dicomSequenceItem;
        }

        /// <summary>
        /// Create a Patient Directory Record
        /// </summary>
        /// <param name="dicomFile">The dicom file or message.</param>
        private static DirectoryRecordSequenceItem CreatePatientItem(DcmDataset dataset)
        {
            if (dataset == null)
                throw new ArgumentNullException("dataset");

            IDictionary<DicomTag, object> dicomTags = new Dictionary<DicomTag, object>();
            dicomTags.Add(DicomTags.PatientsName, null);
            dicomTags.Add(DicomTags.PatientID, null);
            dicomTags.Add(DicomTags.PatientsBirthDate, null);
            dicomTags.Add(DicomTags.PatientsSex, null);

            return AddSequenceItem(DirectoryRecordType.Patient, dataset, dicomTags);
        }

        /// <summary>
        /// Create a Study Directory Record
        /// </summary>
        /// <param name="dicomFile">The dicom file.</param>
        private static DirectoryRecordSequenceItem CreateStudyItem(DcmDataset dataset)
        {
            IDictionary<DicomTag, object> dicomTags = new Dictionary<DicomTag, object>();
            dicomTags.Add(DicomTags.StudyInstanceUID, null);
            dicomTags.Add(DicomTags.StudyID, null);
            dicomTags.Add(DicomTags.StudyDate, null);
            dicomTags.Add(DicomTags.StudyTime, null);
            dicomTags.Add(DicomTags.AccessionNumber, null);
            dicomTags.Add(DicomTags.StudyDescription, null);

            return AddSequenceItem(DirectoryRecordType.Study, dataset, dicomTags);
        }

        /// <summary>
        /// Create a Series Directory Record
        /// </summary>
        /// <param name="dicomFile">The dicom file.</param>
        private static DirectoryRecordSequenceItem CreateSeriesItem(DcmDataset dataset)
        {
            IDictionary<DicomTag, object> dicomTags = new Dictionary<DicomTag, object>();
            dicomTags.Add(DicomTags.SeriesInstanceUID, null);
            dicomTags.Add(DicomTags.Modality, null);
            dicomTags.Add(DicomTags.SeriesDate, null);
            dicomTags.Add(DicomTags.SeriesTime, null);
            dicomTags.Add(DicomTags.SeriesNumber, null);
            dicomTags.Add(DicomTags.SeriesDescription, null);
            //dicomTags.Add(DicomTags.SeriesDescription, dicomFile.DataSet[DicomTags.SeriesDescription].GetString(0, String.Empty));

            return AddSequenceItem(DirectoryRecordType.Series, dataset, dicomTags);
        }

        /// <summary>
        /// Evaluates the relative path to <paramref name="absoluteFilePath"/> from <paramref name="mainDirPath"/>.
        /// </summary>
        /// <param name="mainDirPath">The main dir path.</param>
        /// <param name="absoluteFilePath">The absolute file path.</param>
        /// <returns></returns>
        private static string EvaluateRelativePath(string mainDirPath, string absoluteFilePath)
        {
            string[] firstPathParts = mainDirPath.Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
            string[] secondPathParts = absoluteFilePath.Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);

            int sameCounter = 0;
            for (int i = 0; i < Math.Min(firstPathParts.Length, secondPathParts.Length); i++)
            {
                if (!firstPathParts[i].ToLower().Equals(secondPathParts[i].ToLower()))
                {
                    break;
                }
                sameCounter++;
            }

            if (sameCounter == 0)
            {
                return absoluteFilePath;
            }

            string newPath = String.Empty;

            for (int i = sameCounter; i < firstPathParts.Length; i++)
            {
                if (i > sameCounter)
                {
                    newPath += Path.DirectorySeparatorChar;
                }
                newPath += "..";
            }

            if (newPath.Length == 0)
            {
                newPath = ".";
            }

            for (int i = sameCounter; i < secondPathParts.Length; i++)
            {
                newPath += Path.DirectorySeparatorChar;
                newPath += secondPathParts[i];
            }

            return newPath;
        }
        #endregion

        #region IDisposable Members

        private bool _disposed;
        public void Dispose()
        {
			if (!_disposed)
            {
                if (_dicomDirFile != null)
                    _dicomDirFile = null;

				_disposed = true;
            }
        }

        #endregion
    }

	
	/// <summary>
	/// Dictionary of the directory records required for specific SopClasses.
	/// </summary>
	internal static class DirectoryRecordDictionary
	{
		#region Private Members
		private static Dictionary<string, DirectoryRecordType> _sopClassLookup = new Dictionary<string, DirectoryRecordType>();
        private static Dictionary<DirectoryRecordType, List<DicomTag>> _tagLookupList = new Dictionary<DirectoryRecordType, List<DicomTag>>();
		#endregion

		#region Constructors
		static DirectoryRecordDictionary()
		{
            _sopClassLookup.Add(DicomUID.AmbulatoryECGWaveformStorage.UID, DirectoryRecordType.Waveform);
            _sopClassLookup.Add(DicomUID.BasicTextSRStorage.UID, DirectoryRecordType.SrDocument);
            _sopClassLookup.Add(DicomUID.BasicVoiceAudioWaveformStorage.UID, DirectoryRecordType.Waveform);
            _sopClassLookup.Add(DicomUID.BlendingSoftcopyPresentationStateStorageSOPClass.UID, DirectoryRecordType.Presentation);
            _sopClassLookup.Add(DicomUID.CardiacElectrophysiologyWaveformStorage.UID, DirectoryRecordType.Waveform);
            _sopClassLookup.Add(DicomUID.ChestCADSRStorage.UID, DirectoryRecordType.SrDocument);
            _sopClassLookup.Add(DicomUID.ColorSoftcopyPresentationStateStorageSOPClass.UID, DirectoryRecordType.Presentation);
            _sopClassLookup.Add(DicomUID.ComprehensiveSRStorage.UID, DirectoryRecordType.SrDocument);
            _sopClassLookup.Add(DicomUID.ComputedRadiographyImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.CTImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.DeformableSpatialRegistrationStorage.UID, DirectoryRecordType.Registration);
            _sopClassLookup.Add(DicomUID.DigitalIntraoralXRayImageStorageForPresentation.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.DigitalIntraoralXRayImageStorageForProcessing.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.DigitalMammographyXRayImageStorageForPresentation.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.DigitalMammographyXRayImageStorageForProcessing.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.DigitalXRayImageStorageForPresentation.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.EncapsulatedCDAStorage.UID, DirectoryRecordType.Hl7StrucDoc);
            _sopClassLookup.Add(DicomUID.EncapsulatedPDFStorage.UID, DirectoryRecordType.EncapDoc);
            _sopClassLookup.Add(DicomUID.EnhancedCTImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.EnhancedMRImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.EnhancedSRStorage.UID, DirectoryRecordType.SrDocument);
            _sopClassLookup.Add(DicomUID.EnhancedXAImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.EnhancedXRFImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.GeneralECGWaveformStorage.UID, DirectoryRecordType.Waveform);
            _sopClassLookup.Add(DicomUID.GrayscaleSoftcopyPresentationStateStorageSOPClass.UID, DirectoryRecordType.Presentation);
            _sopClassLookup.Add(DicomUID.HangingProtocolStorage.UID, DirectoryRecordType.HangingProtocol);
            _sopClassLookup.Add(DicomUID.HemodynamicWaveformStorage.UID, DirectoryRecordType.Waveform);
            _sopClassLookup.Add(DicomUID.KeyObjectSelectionDocumentStorage.UID, DirectoryRecordType.KeyObjectDoc);
            _sopClassLookup.Add(DicomUID.MammographyCADSRStorage.UID, DirectoryRecordType.SrDocument);
            _sopClassLookup.Add(DicomUID.MRImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.MRSpectroscopyStorage.UID, DirectoryRecordType.Spectroscopy);
            _sopClassLookup.Add(DicomUID.MultiframeGrayscaleByteSecondaryCaptureImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.MultiframeGrayscaleWordSecondaryCaptureImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.MultiframeSingleBitSecondaryCaptureImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.MultiframeTrueColorSecondaryCaptureImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.NuclearMedicineImageStorageRETIRED.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.NuclearMedicineImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.OphthalmicPhotography16BitImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.OphthalmicPhotography8BitImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.OphthalmicTomographyImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.PositronEmissionTomographyImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.PseudoColorSoftcopyPresentationStateStorageSOPClass.UID, DirectoryRecordType.Presentation);
            _sopClassLookup.Add(DicomUID.RawDataStorage.UID, DirectoryRecordType.RawData);
            _sopClassLookup.Add(DicomUID.RealWorldValueMappingStorage.UID, DirectoryRecordType.ValueMap);
            _sopClassLookup.Add(DicomUID.RTBeamsTreatmentRecordStorage.UID, DirectoryRecordType.RtTreatRecord);
            _sopClassLookup.Add(DicomUID.RTBrachyTreatmentRecordStorage.UID, DirectoryRecordType.RtTreatRecord);
            _sopClassLookup.Add(DicomUID.RTDoseStorage.UID, DirectoryRecordType.RtDose);
            _sopClassLookup.Add(DicomUID.RTImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.RTIonBeamsTreatmentRecordStorage.UID, DirectoryRecordType.RtTreatRecord);
            _sopClassLookup.Add(DicomUID.RTIonPlanStorage.UID, DirectoryRecordType.RtPlan);
            _sopClassLookup.Add(DicomUID.RTPlanStorage.UID, DirectoryRecordType.RtPlan);
            _sopClassLookup.Add(DicomUID.RTStructureSetStorage.UID, DirectoryRecordType.RtStructureSet);
            _sopClassLookup.Add(DicomUID.RTTreatmentSummaryRecordStorage.UID, DirectoryRecordType.RtTreatRecord);
            _sopClassLookup.Add(DicomUID.SecondaryCaptureImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.SegmentationStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.SpatialFiducialsStorage.UID, DirectoryRecordType.Fiducial);
            _sopClassLookup.Add(DicomUID.SpatialRegistrationStorage.UID, DirectoryRecordType.Registration);
            _sopClassLookup.Add(DicomUID.StereometricRelationshipStorage.UID, DirectoryRecordType.Stereometric);
            _sopClassLookup.Add(DicomUID.SubstanceAdministrationLoggingSOPClass.UID, DirectoryRecordType.SrDocument);
            _sopClassLookup.Add(DicomUID.UltrasoundImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.UltrasoundImageStorageRETIRED.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.UltrasoundMultiframeImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.UltrasoundMultiframeImageStorageRETIRED.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.VideoEndoscopicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.VideoMicroscopicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.VideoPhotographicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.VLEndoscopicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.VLMicroscopicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.VLPhotographicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.VLSlideCoordinatesMicroscopicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.XRay3DAngiographicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.XRay3DCraniofacialImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.XRayAngiographicBiPlaneImageStorageRETIRED.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.XRayAngiographicImageStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.XRayRadiationDoseSRStorage.UID, DirectoryRecordType.Image);
            _sopClassLookup.Add(DicomUID.XRayRadiofluoroscopicImageStorage.UID, DirectoryRecordType.Image);

			// At some point this will need to be improved to make the 
			// the DICOMDIRs compliant.  IE, we're not looking at Type2 and conditional tags
			// properly and inserting them.

			//RT DOSE
            List<DicomTag> tagList = new List<DicomTag>();
			tagList.Add(DicomTags.InstanceNumber);
			tagList.Add(DicomTags.DoseSummationType);
			_tagLookupList.Add(DirectoryRecordType.RtDose, tagList);

			//RT STRUCTURE SET
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.InstanceNumber,
			          		DicomTags.StructureSetLabel,
			          		DicomTags.StructureSetDate,
			          		DicomTags.StructureSetTime
			          	};
			_tagLookupList.Add(DirectoryRecordType.RtStructureSet, tagList);

			//RT PLAN
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.InstanceNumber,
			          		DicomTags.RTPlanLabel,
			          		DicomTags.RTPlanDate,
			          		DicomTags.RTPlanTime
			          	};
			_tagLookupList.Add(DirectoryRecordType.RtPlan, tagList);

			//RT TREAT RECORD
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.InstanceNumber,
			          		//TODO Some of the 0x3008 group tags are not in the dictionary, see ticket #5162
			          	};
			_tagLookupList.Add(DirectoryRecordType.RtTreatRecord, tagList);

			//PRESENTATION
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.PresentationCreationDate,
							DicomTags.PresentationCreationTime,
							DicomTags.ReferencedSeriesSequence,
							DicomTags.BlendingSequence
			          	};
			_tagLookupList.Add(DirectoryRecordType.Presentation, tagList);

			//WAVEFORM
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.InstanceNumber,
							DicomTags.ContentDate,
							DicomTags.ContentTime
			          	};
			_tagLookupList.Add(DirectoryRecordType.Waveform, tagList);

			//SR DOCUMENT
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.InstanceNumber,
							DicomTags.CompletionFlag,
							DicomTags.VerificationFlag,
							DicomTags.ContentDate,
							DicomTags.ContentTime,
							DicomTags.VerificationDateTime,
							DicomTags.ConceptNameCodeSequence
			          	};
			_tagLookupList.Add(DirectoryRecordType.SrDocument, tagList);

			//KEY OBJECT DOC
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.InstanceNumber,
							DicomTags.ContentDate,
							DicomTags.ContentTime,
							DicomTags.ConceptNameCodeSequence
			          	};
			_tagLookupList.Add(DirectoryRecordType.KeyObjectDoc, tagList);

			//SPECTROSCOPY
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.ImageType,
							DicomTags.ContentDate,
							DicomTags.ContentTime,
							DicomTags.InstanceNumber,
							DicomTags.NumberOfFrames,
							DicomTags.Rows,
							DicomTags.Columns,
							DicomTags.DataPointRows,
							DicomTags.DataPointColumns

			          	};
			_tagLookupList.Add(DirectoryRecordType.Spectroscopy, tagList);

			//RAW DATA
            tagList = new List<DicomTag>
			          	{
							DicomTags.ContentDate,
							DicomTags.ContentTime,
							DicomTags.InstanceNumber

			          	};
			_tagLookupList.Add(DirectoryRecordType.RawData, tagList);

			//REGISTRATION
            tagList = new List<DicomTag>
			          	{
							DicomTags.ContentDate,
							DicomTags.ContentTime,
							DicomTags.InstanceNumber,
							DicomTags.ContentLabel,
							DicomTags.ContentDescription,
							DicomTags.ContentCreatorsName,
							DicomTags.PersonIdentificationCodeSequence

			          	};
			_tagLookupList.Add(DirectoryRecordType.Registration, tagList);

			//FUDICIAL
            tagList = new List<DicomTag>
			          	{
							DicomTags.ContentDate,
							DicomTags.ContentTime,
							DicomTags.InstanceNumber,
							DicomTags.ContentLabel,
							DicomTags.ContentDescription,
							DicomTags.ContentCreatorsName,
							DicomTags.PersonIdentificationCodeSequence

			          	};
			_tagLookupList.Add(DirectoryRecordType.Fiducial, tagList);

			//HANGING PROTOCOL
            tagList = new List<DicomTag>
			          	{
							DicomTags.HangingProtocolName,
							DicomTags.HangingProtocolDescription,
							DicomTags.HangingProtocolLevel,
							DicomTags.HangingProtocolCreator,
							DicomTags.HangingProtocolCreationDateTime,
							DicomTags.HangingProtocolDefinitionSequence,
							DicomTags.NumberOfPriorsReferenced,
							DicomTags.HangingProtocolUserIdentificationCodeSequence
			          	};
			_tagLookupList.Add(DirectoryRecordType.HangingProtocol, tagList);

			//ENCAP DOC
            tagList = new List<DicomTag>
			          	{
							DicomTags.ContentDate,
							DicomTags.ContentTime,
							DicomTags.InstanceNumber,
							DicomTags.DocumentTitle,
							DicomTags.MIMETypeOfEncapsulatedDocument
			          	};
			_tagLookupList.Add(DirectoryRecordType.EncapDoc, tagList);


			//HL7 STRUC DOC
            tagList = new List<DicomTag>
			          	{
							DicomTags.HL7InstanceIdentifier,
							DicomTags.HL7DocumentEffectiveTime
			          	};
			_tagLookupList.Add(DirectoryRecordType.Hl7StrucDoc, tagList);

			//VALUE MAP
            tagList = new List<DicomTag>
			          	{
			          		DicomTags.ContentDate,
			          		DicomTags.ContentTime,
			          		DicomTags.InstanceNumber,
			          		DicomTags.ContentLabel,
			          		DicomTags.ContentDescription,
			          		DicomTags.ContentCreatorsName,
			          		DicomTags.PersonIdentificationCodeSequence
			          	};
			_tagLookupList.Add(DirectoryRecordType.ValueMap, tagList);

			//STEREOMETRIC
            tagList = new List<DicomTag>();
			_tagLookupList.Add(DirectoryRecordType.Stereometric, tagList);

			//PRIVATE
            tagList = new List<DicomTag>();
			_tagLookupList.Add(DirectoryRecordType.Private, tagList);

		}
		#endregion

		#region Methods
		/// <summary>
		/// Get the <see cref="DirectoryRecordType"/> for a given SopClass UID.
		/// </summary>
		/// <param name="uid">The SOP Class UID string.</param>
		/// <param name="type">The output directory record type.</param>
		/// <returns>Returns true if the directory record type is found, or else false.</returns>
		internal static bool TryGetDirectoryRecordType(string uid, out DirectoryRecordType type)
		{
			return _sopClassLookup.TryGetValue(uid, out type);
		}

		/// <summary>
		/// Get a list of tags to be populated into a <see cref="DirectoryRecordSequenceItem"/> for the 
		/// specified <see cref="DirectoryRecordType"/>.
		/// </summary>
		/// <param name="type">The directory record type to get the tag list for.</param>
		/// <param name="tagList">The list of tags to be included.</param>
		/// <returns></returns>
        internal static bool TryGetDirectoryRecordTagList(DirectoryRecordType type, out List<DicomTag> tagList)
		{
			return _tagLookupList.TryGetValue(type, out tagList);
		}
		#endregion
	}
}
