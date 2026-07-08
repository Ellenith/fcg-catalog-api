// ============================================================
// CatalogDbContext.cs — Banco próprio do CatalogAPI
// Arquivo: catalog.db (SQLite)
// ============================================================

using FCG.CatalogAPI.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace FCG.CatalogAPI.Infraestrutura.Persistencia;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options) { }

    public DbSet<Jogo>      Jogos      { get; set; }
    public DbSet<Promocao>  Promocoes  { get; set; }
    public DbSet<Biblioteca> Biblioteca { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Jogo>(entity =>
        {
            entity.ToTable("Jogos");
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Titulo).IsRequired().HasMaxLength(200);
            entity.Property(j => j.Descricao).IsRequired().HasMaxLength(2000);
            entity.Property(j => j.Preco).IsRequired().HasPrecision(18, 2);
            entity.Property(j => j.CriadoEm).IsRequired();
        });

        modelBuilder.Entity<Promocao>(entity =>
        {
            entity.ToTable("Promocoes");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.PercentualDesconto).IsRequired().HasPrecision(5, 2);
            entity.Property(p => p.Inicio).IsRequired();
            entity.Property(p => p.Fim).IsRequired();
            entity.HasOne<Jogo>()
                .WithMany(j => j.Promocoes)
                .HasForeignKey(p => p.JogoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Biblioteca>(entity =>
        {
            entity.ToTable("Biblioteca");
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.UsuarioId, b.JogoId }).IsUnique();
            entity.HasIndex(b => b.OrderId).IsUnique();
            entity.Property(b => b.PrecoPago).IsRequired().HasPrecision(18, 2);
            entity.Property(b => b.AdquiridoEm).IsRequired();
        });
    }
}