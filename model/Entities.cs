using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMaier.SimpleDlna.Model

{
  //[Table("Media")]
  public abstract class BaseFile
  {
    [Key]
    public int Id { get; set; }
    [Required]
    public string Title { get; set; }
    public SimpleDlna.Server.DlnaMediaTypes MediaType { get; set; }
    public SimpleDlna.Server.DlnaMime Type { get; set; }

    public long? SizeRaw { get; set; }

    public string Size { get; set; }
    public DateTime Date { get; set; }
    public string DateO { get; set; }
    //public bool HasCover { get; set; }

    public int? CoverId { get; set; }
    public virtual Cover Cover { get; set; }


    
    [Required]
    [Index(IsUnique = true)]
    public string Path { get; set; }


  }
  //[Table("Video")]
  public class VideoFile : BaseFile
  {
    public string Actors { get; set; }
    public string Description { get; set; }
    public string Director { get; set; }
    public string Genre { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? Bookmark { get; set; }
    public long Duration { get; set; }
    public long Progress { get; set; }

    public bool HasInternalSubtitle { get; set; }
    public bool IsInternalSubtitleASS { get; set; }

    public int? TVDBId { get; set; }
    public virtual TVDB TVDB { get; set; }
    public virtual ICollection<Subtitle> Subtitles { get; set; }
  }
  //[Table("Audio")]
  public class AudioFile : BaseFile
  {
    public string Album { get; set; }
    public string Artist { get; set; }
    public string Genre { get; set; }
    public string Performer { get; set; }

    public int? Track { get; set; }

    public long Duration { get; set; }

  }
  //[Table("Image")]
  public class ImageFile : BaseFile
  {
    public string Creator { get; set; }
    public string Description { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }
  }

  [Table("TVShow")]
  public class TVDB {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Name { get; set; }

    public long LastUpdated { get; set; }

    public string IMDB { get; set; }

    public virtual ICollection<VideoFile> VideoFiles { get; set; }
    public virtual ICollection<TVDBEntry> Entries { get; set; }
  }

  [Table("TVSHowEntry")]
  public class TVDBEntry
  {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Index("IX_TVSHowEntry_U", 1, IsUnique = true)]
    //[Column("Id")]
    public int TVDBId { get; set; }

    [Index("IX_TVSHowEntry_U", 2, IsUnique = true)]
    public int Season { get; set; }
    [Index("IX_TVSHowEntry_U", 3, IsUnique = true)]
    public int Episode { get; set; }

    public string Title { get; set; }
    public DateTime Aired { get; set; }

    public int EpisodeId { get; set; }

    public int AbsoluteNumber { get; set; }

    public virtual TVDB TVDB { get; set; }
  }

  public class Subtitle
  {
    [Key]
    public int Id { get; set; }
    public string Path { get; set; }
    public bool Internal { get; set; }
    public string Data { get; set; }

    public DateTime Modified { get; set; }

    public int VideoFileId { get; set; }
    public virtual VideoFile VideoFile { get; set; }
  }
  public class Cover
  {
    [Key]
    public int Id { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public byte[] Data { get; set; }

    public virtual BaseFile File { get; set; }


  }
}
