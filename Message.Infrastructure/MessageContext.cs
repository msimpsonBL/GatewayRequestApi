﻿using MediatR;
using Message.Domain.MessageAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Microsoft.EntityFrameworkCore.Design;
using Message.Infrastructure.EntityConfigurations;

namespace Message.Infrastructure;

public class MessageContext : DbContext, IUnitOfWork
{

    public const string DEFAULT_SCHEMA = "dbo";
    public virtual DbSet<CommonMessage> Common { get; set; }
    public virtual DbSet<messageTypeLookup> messageTypeLookups { get; set; }
    public virtual DbSet<ReaMessage> REAs { get; set; }
    public virtual DbSet<RsiMessage> RSIs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.ApplyConfiguration(new RsiMessageEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ReaMessageEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new CommonEntityTypeConfiguration());

        modelBuilder.Entity<messageTypeLookup>()
            .Property(e => e.type)
            .IsUnicode(false);

        modelBuilder.Entity<messageTypeLookup>()
            .HasMany(e => e.Commons)
            .WithOne(e => e.messageTypeLookup).IsRequired()
            .HasForeignKey(e => e.m_type)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private readonly IMediator _mediator;
    private IDbContextTransaction _currentTransaction;
    public MessageContext(DbContextOptions<MessageContext> options) : base(options) { }
    public MessageContext(DbContextOptions<MessageContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }
    public IDbContextTransaction GetCurrentTransaction() => _currentTransaction;
    public bool HasActiveTransaction => _currentTransaction != null;

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch Domain Events collection. 
        // Choices:
        // A) Right BEFORE committing data (EF SaveChanges) into the DB will make a single transaction including  
        // side effects from the domain event handlers which are using the same DbContext with "InstancePerLifetimeScope" or "scoped" lifetime
        // B) Right AFTER committing data (EF SaveChanges) into the DB will make multiple transactions. 
        // You will need to handle eventual consistency and compensatory actions in case of failures in any of the Handlers. 
        await _mediator.DispatchDomainEventsAsync(this);

        // After executing this line all the changes (from the Command Handler and Domain Event Handlers) 
        // performed through the DbContext will be committed
        var result = await base.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        if (_currentTransaction != null) return null;

        _currentTransaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(IDbContextTransaction transaction)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));
        if (transaction != _currentTransaction) throw new InvalidOperationException($"Transaction {transaction.TransactionId} is not current");

        try
        {
            await SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            RollbackTransaction();
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public void RollbackTransaction()
    {
        try
        {
            _currentTransaction?.Rollback();
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }
}
