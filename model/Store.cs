using SQLite.CodeFirst;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.Entity.SqlServer;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMaier.SimpleDlna.Model
{
  public class Store : DbContext
  {
    public DbSet<VideoFile> Videos { get; set; }
    public DbSet<AudioFile> Audios { get; set; }
    public DbSet<ImageFile> Images { get; set; }
    public DbSet<TVDB> TVDBs { get; set; }
    public DbSet<TVDBEntry> TVDBEntries { get; set; }

    public DbSet<Cover> Covers { get; set; }
    public DbSet<Subtitle> Subtitles { get; set; }

    public DbSet<BaseFile> Files { get; set; }

    public Store(System.IO.FileInfo dbpath) : base(new SQLiteConnection() { ConnectionString = new SQLiteConnectionStringBuilder() { DataSource = dbpath.FullName, ForeignKeys = true }.ConnectionString }, true) { }

    public Store() : this(new System.IO.FileInfo("cache2.sqlite")) { }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {


      //modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

      //modelBuilder.Entity<TVDBEntry>().HasRequired<Model.TVDB>(t => t.TVDB).WithMany(t => t.Entries).Map(m => m.MapKey("Id"));//.HasForeignKey(t => t.Id);

      modelBuilder.Entity<Cover>().HasRequired(c => c.File).WithOptional(f => f.Cover);

      modelBuilder.Entity<Subtitle>().HasOptional(s => s.VideoFile).WithOptionalDependent().WillCascadeOnDelete();

      //modelBuilder.Entity<BaseFile>().HasOptional(f => f.Cover).WithRequired(c => c.File);
      modelBuilder.Entity<BaseFile>()
        .Map<VideoFile>(m => { m.Requires("Actors"); m.Requires("Description"); m.Requires("Director"); m.Requires("Genre"); m.Requires("Width"); m.Requires("Height"); m.Requires("Bookmark"); m.Requires("Duration"); m.Requires("TVDBId"); m.Requires("TVDBEntryId"); })
        .Map<AudioFile>(m => { m.Requires("Album"); m.Requires("Artist"); m.Requires("Genre"); m.Requires("Performer"); m.Requires("Duration"); })
        .Map<ImageFile>(m => { m.Requires("Creator"); m.Requires("Description"); m.Requires("Width"); m.Requires("Height"); });

      var sqliteConnectionInitializer = new SqliteCreateDatabaseIfNotExists<Store>(modelBuilder);
      Database.SetInitializer(sqliteConnectionInitializer);
    }

  }
}
