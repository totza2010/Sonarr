using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    public class UpgradeMediaFileServiceFixture : CoreTest<UpgradeMediaFileService>
    {
        private EpisodeFile _episodeFile;
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode();
            _localEpisode.Series = new Series
                                   {
                                       Path = @"C:\Test\TV\Series".AsOsAgnostic()
                                   };

            _episodeFile = Builder<EpisodeFile>
                .CreateNew()
                .Build();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FolderExists(Directory.GetParent(_localEpisode.Series.Path).FullName))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.GetParentFolder(It.IsAny<string>()))
                  .Returns<string>(c => Path.GetDirectoryName(c));
        }

        private void GivenSingleEpisodeWithSingleEpisodeFile()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    RelativePath = @"Season 01\30.rock.s01e01.avi",
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        private void GivenMultipleEpisodesWithSingleEpisodeFile()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(2)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    RelativePath = @"Season 01\30.rock.s01e01.avi",
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        private void GivenMultipleEpisodesWithMultipleEpisodeFiles()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(2)
                                                     .TheFirst(1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    RelativePath = @"Season 01\30.rock.s01e01.avi",
                                                                                })
                                                     .TheNext(1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 2,
                                                                                    RelativePath = @"Season 01\30.rock.s01e02.avi",
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        private void GivenExistingMultiple(EpisodeFileMultipleType multipleType, int multipleNumber)
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    MultipleType = multipleType,
                                                                                    MultipleNumber = multipleNumber,
                                                                                    RelativePath = @"Season 01.rock.s01e01.pt1.avi",
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        [Test]
        public void should_not_delete_a_different_part_of_the_same_episode()
        {
            // The whole point: importing part 2 must leave part 1 where it is.
            GivenExistingMultiple(EpisodeFileMultipleType.Part, 1);
            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 2;

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_replace_the_same_part()
        {
            // Re-importing part 1 is still a replacement, otherwise duplicates pile up.
            GivenExistingMultiple(EpisodeFileMultipleType.Part, 1);
            _localEpisode.MultipleType = EpisodeFileMultipleType.Part;
            _localEpisode.MultipleNumber = 1;

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_delete_every_part_when_importing_an_ordinary_release()
        {
            // A plain release supersedes the whole episode, so it replaces all of its parts rather than
            // only the one the episode points at, which would leave the rest behind from the old release.
            GivenExistingMultiple(EpisodeFileMultipleType.Part, 1);

            Mocker.GetMock<IEpisodeFileLinkService>()
                  .Setup(s => s.GetLinkedFileIds(It.IsAny<List<int>>()))
                  .Returns(new List<int> { 2 });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.Get(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<EpisodeFile>
                           {
                               new EpisodeFile { Id = 2, MultipleType = EpisodeFileMultipleType.Part, MultipleNumber = 2, RelativePath = @"Season 01\30.rock.s01e01.pt2.avi" }
                           });

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Test]
        public void should_not_delete_a_different_version_of_the_same_episode()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();
            _localEpisode.Episodes.First().EpisodeFile.Value.MultipleType = EpisodeFileMultipleType.Version;
            _localEpisode.Episodes.First().EpisodeFile.Value.MultipleNumber = 1;
            _localEpisode.MultipleType = EpisodeFileMultipleType.Version;
            _localEpisode.MultipleNumber = 2;

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_replace_the_same_version()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();
            _localEpisode.Episodes.First().EpisodeFile.Value.MultipleType = EpisodeFileMultipleType.Version;
            _localEpisode.Episodes.First().EpisodeFile.Value.MultipleNumber = 2;
            _localEpisode.MultipleType = EpisodeFileMultipleType.Version;
            _localEpisode.MultipleNumber = 2;

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_not_replace_a_part_with_a_version_of_the_same_number()
        {
            // Sharing a number is not sharing an identity: part 1 and version 1 are different files.
            GivenExistingMultiple(EpisodeFileMultipleType.Part, 1);
            _localEpisode.MultipleType = EpisodeFileMultipleType.Version;
            _localEpisode.MultipleNumber = 1;

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_still_replace_a_part_when_the_import_says_nothing_about_parts()
        {
            // An ordinary upgrade has no part or version, so it replaces whatever is there.
            GivenExistingMultiple(EpisodeFileMultipleType.Part, 1);

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_delete_single_episode_file_once()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_delete_the_same_episode_file_only_once()
        {
            GivenMultipleEpisodesWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_delete_multiple_different_episode_files()
        {
            GivenMultipleEpisodesWithMultipleEpisodeFiles();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Test]
        public void should_delete_episode_file_from_database()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(It.IsAny<EpisodeFile>(), DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_delete_existing_file_fromdb_if_file_doesnt_exist()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localEpisode.Episodes.Single().EpisodeFile, DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_not_try_to_recyclebin_existing_file_if_file_doesnt_exist()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_return_old_episode_file_in_oldFiles()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode).OldFiles.Count.Should().Be(1);
        }

        [Test]
        public void should_return_old_episode_files_in_oldFiles()
        {
            GivenMultipleEpisodesWithMultipleEpisodeFiles();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode).OldFiles.Count.Should().Be(2);
        }

        [Test]
        public void should_throw_if_there_are_existing_episode_files_and_the_root_folder_is_missing()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FolderExists(Directory.GetParent(_localEpisode.Series.Path).FullName))
                  .Returns(false);

            Assert.Throws<RootFolderNotFoundException>(() => Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode));

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localEpisode.Episodes.Single().EpisodeFile, DeleteMediaFileReason.Upgrade), Times.Never());
        }

        [Test]
        public void should_import_if_existing_file_doesnt_exist_in_db()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new LazyLoaded<EpisodeFile>(null))
                                                     .Build()
                                                     .ToList();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localEpisode.Episodes.Single().EpisodeFile, It.IsAny<DeleteMediaFileReason>()), Times.Never());
        }
    }
}
