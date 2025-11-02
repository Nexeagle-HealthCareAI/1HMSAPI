using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Domain.Entities;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Context
{
    [ExcludeFromCodeCoverage]
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<LookupType> LookupTypes { get; set; }
        public DbSet<LookupMaster> LookupMasters { get; set; }
        public DbSet<LookupPersonal> LookupPersonals { get; set; }
        public DbSet<DoctorPreferredMedicine> DoctorPreferredMedicines { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserAuth> UserAuths { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Hospital> Hospitals { get; set; }
        public DbSet<HospitalUser> HospitalUsers { get; set; }
        public DbSet<HospitalProfileStatus> HospitalProfileStatuses { get; set; }
        public DbSet<HospitalSetting> HospitalSettings { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<HospitalDepartmentMapping> HospitalDepartmentMappings { get; set; }
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<DoctorDepartment> DoctorDepartments { get; set; }
        public DbSet<DoctorSpecialization> DoctorSpecializations { get; set; }
        public DbSet<PrescriptionHeaderFooter> PrescriptionHeaderFooters { get; set; }
        public DbSet<UserInvitation> UserInvitations { get; set; }
        public DbSet<DoctorShiftTemplate> DoctorShiftTemplates { get; set; }
        public DbSet<DoctorShiftOverride> DoctorShiftOverrides { get; set; }
        public DbSet<DoctorTimeOff> DoctorTimeOffs { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<AppointmentToken> AppointmentTokens { get; set; }
        public DbSet<AppointmentVitals> AppointmentVitals { get; set; }
        public DbSet<DoctorQueue> DoctorQueues { get; set; }
        public DbSet<PatientRegistration> PatientRegistrations { get; set; }
        public DbSet<StatusMaster> StatusMasters { get; set; }
        public DbSet<PrescriptionAsset> PrescriptionAssets { get; set; }
        public DbSet<PrescriptionSetting> PrescriptionSettings { get; set; }
        public DbSet<DoctorSectionPreference> DoctorSectionPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {   
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<UserAuth>().ToTable("UserAuth");
            modelBuilder.Entity<UserProfile>().ToTable("UserProfiles");
            
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<Permission>().ToTable("Permissions");
            modelBuilder.Entity<RolePermission>().ToTable("RolePermissions");
            modelBuilder.Entity<UserRole>().ToTable("UserRoles");

            modelBuilder.Entity<Hospital>().ToTable("Hospitals");
            modelBuilder.Entity<HospitalUser>().ToTable("HospitalUsers");
            modelBuilder.Entity<HospitalProfileStatus>().ToTable("HospitalProfileStatus");
            modelBuilder.Entity<HospitalSetting>().ToTable("HospitalSettings");

            modelBuilder.Entity<Department>().ToTable("Departments");
            modelBuilder.Entity<HospitalDepartmentMapping>().ToTable("HospitalDepartmentMappings");
            modelBuilder.Entity<Specialization>().ToTable("Specializations");

            // Lookup entities
            modelBuilder.Entity<LookupType>().ToTable("LookupTypes");
            modelBuilder.Entity<LookupMaster>().ToTable("LookupMasters");
            modelBuilder.Entity<LookupPersonal>().ToTable("LookupPersonals");

            // Keys and relationships for lookup entities
            modelBuilder.Entity<LookupType>().HasKey(e => e.LookupTypeId);
            modelBuilder.Entity<LookupMaster>().HasKey(e => e.LookupId);
            modelBuilder.Entity<LookupPersonal>().HasKey(e => e.PersonalId);

            modelBuilder.Entity<LookupMaster>()
                .HasOne(lm => lm.LookupType)
                .WithMany(lt => lt.LookupMasters)
                .HasForeignKey(lm => lm.LookupTypeId);

            modelBuilder.Entity<LookupPersonal>()
                .HasOne(lp => lp.LookupType)
                .WithMany(lt => lt.LookupPersonals)
                .HasForeignKey(lp => lp.LookupTypeId);

            modelBuilder.Entity<LookupPersonal>()
                .HasOne(lp => lp.MasterLookup)
                .WithMany(lm => lm.LookupPersonals)
                .HasForeignKey(lp => lp.MasterLookupId);

            modelBuilder.Entity<Doctor>().ToTable("Doctors");
            modelBuilder.Entity<DoctorDepartment>().ToTable("DoctorDepartments");
            modelBuilder.Entity<DoctorSpecialization>().ToTable("DoctorSpecializations");
            modelBuilder.Entity<PrescriptionHeaderFooter>().ToTable("PrescriptionHeaderFooter");
            modelBuilder.Entity<UserInvitation>().ToTable("UserInvitations");
            modelBuilder.Entity<DoctorShiftTemplate>().ToTable("DoctorShiftTemplates");
            modelBuilder.Entity<DoctorShiftOverride>().ToTable("DoctorShiftOverrides");
            modelBuilder.Entity<DoctorTimeOff>().ToTable("DoctorTimeOffs");
            modelBuilder.Entity<UserProfile>(entity =>
            {

                entity.Property(u => u.ProfileCompletionPercentage)
                      .HasColumnName("ProfileCompletionPercent")
                      .HasDefaultValue(0);
            });

            modelBuilder.Entity<Doctor>(entity =>
            {

                entity.Property(d => d.ProfileCompletionPercent).HasPrecision(5, 2);
            });

            modelBuilder.Entity<User>().HasKey(e => e.UserID);
            modelBuilder.Entity<UserAuth>().HasKey(e => e.UserAuthID);
            modelBuilder.Entity<UserProfile>().HasKey(e => e.UserProfileID);
            modelBuilder.Entity<UserInvitation>().HasKey(e => e.InvitationID);
            
            modelBuilder.Entity<Role>().HasKey(e => e.RoleID);
            modelBuilder.Entity<Permission>().HasKey(e => e.PermissionKey);
            modelBuilder.Entity<RolePermission>().HasKey(e => new { e.RoleID, e.PermissionKey });
            modelBuilder.Entity<UserRole>().HasKey(e => new { e.UserID, e.RoleID });

            modelBuilder.Entity<Hospital>().HasKey(e => e.HospitalID);
            modelBuilder.Entity<HospitalUser>().HasKey(e => e.HospitalUserID);
            modelBuilder.Entity<HospitalProfileStatus>().HasKey(e => e.HospitalID);
            modelBuilder.Entity<HospitalSetting>().HasKey(e => e.HospitalID);

            modelBuilder.Entity<Department>().HasKey(e => e.DepartmentID);
            modelBuilder.Entity<HospitalDepartmentMapping>().HasKey(e => e.MappingID);
            modelBuilder.Entity<Specialization>().HasKey(e => e.SpecializationID);

            modelBuilder.Entity<Doctor>().HasKey(e => e.DoctorID);

            modelBuilder.Entity<DoctorDepartment>().HasKey(e => e.DoctorDepartmentID);
            modelBuilder.Entity<DoctorSpecialization>().HasKey(e => e.DoctorSpecializationID);

            modelBuilder.Entity<PrescriptionHeaderFooter>().HasKey(e => e.PrescriptionTemplateID);
            modelBuilder.Entity<DoctorShiftTemplate>().HasKey(e => e.TemplateID);
            modelBuilder.Entity<DoctorShiftOverride>().HasKey(e => e.OverrideID);
            modelBuilder.Entity<DoctorTimeOff>().HasKey(e => e.TimeOffID);
            modelBuilder.Entity<PrescriptionAsset>().HasKey(e => e.PrescriptionAssetId);
            modelBuilder.Entity<PrescriptionSetting>().HasKey(e => e.PrescriptionSettingId);


            modelBuilder.Entity<UserAuth>()
                .HasOne(ua => ua.User)
                .WithMany(u => u.UserAuths)
                .HasForeignKey(ua => ua.UserID);

            modelBuilder.Entity<UserProfile>()
                .HasOne(up => up.User)
                .WithMany(u => u.UserProfiles)
                .HasForeignKey(up => up.UserID);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleID);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionKey);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserID);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleID);


            modelBuilder.Entity<Hospital>()
                .HasOne(h => h.CreatedByUser)
                .WithMany(u => u.CreatedHospitals)
                .HasForeignKey(h => h.CreatedByUserID);

            modelBuilder.Entity<HospitalUser>()
                .HasOne(hu => hu.Hospital)
                .WithMany(h => h.HospitalUsers)
                .HasForeignKey(hu => hu.HospitalID);

            modelBuilder.Entity<HospitalUser>()
                .HasOne(hu => hu.User)
                .WithMany(u => u.HospitalUsers)
                .HasForeignKey(hu => hu.UserID);

            modelBuilder.Entity<HospitalProfileStatus>()
                .HasOne(hps => hps.Hospital)
                .WithOne(h => h.HospitalProfileStatus)
                .HasForeignKey<HospitalProfileStatus>(hps => hps.HospitalID);

            modelBuilder.Entity<HospitalSetting>()
                .HasOne(hs => hs.Hospital)
                .WithOne(h => h.HospitalSetting)
                .HasForeignKey<HospitalSetting>(hs => hs.HospitalID);


            modelBuilder.Entity<Department>()
                .HasOne(d => d.CreatedByUser)
                .WithMany(u => u.CreatedDepartments)
                .HasForeignKey(d => d.CreatedByUserID);

            modelBuilder.Entity<HospitalDepartmentMapping>()
                .HasOne(hdm => hdm.Hospital)
                .WithMany(h => h.HospitalDepartmentMappings)
                .HasForeignKey(hdm => hdm.HospitalID);

            modelBuilder.Entity<HospitalDepartmentMapping>()
                .HasOne(hdm => hdm.Department)
                .WithMany(d => d.HospitalDepartmentMappings)
                .HasForeignKey(hdm => hdm.DepartmentID);


            modelBuilder.Entity<Specialization>()
                .HasOne(s => s.CreatedByUser)
                .WithMany(u => u.CreatedSpecializations)
                .HasForeignKey(s => s.CreatedByUserID);


            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.User)
                .WithMany(u => u.Doctors)
                .HasForeignKey(d => d.UserID);

            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.PrimaryDepartment)
                .WithMany()
                .HasForeignKey(d => d.PrimaryDepartmentID);

            modelBuilder.Entity<DoctorDepartment>()
                .HasOne(dd => dd.Doctor)
                .WithMany(d => d.DoctorDepartments)
                .HasForeignKey(dd => dd.DoctorID);

            modelBuilder.Entity<DoctorDepartment>()
                .HasOne(dd => dd.Department)
                .WithMany(d => d.DoctorDepartments)
                .HasForeignKey(dd => dd.DepartmentID);

            modelBuilder.Entity<DoctorSpecialization>()
                .HasOne(ds => ds.Doctor)
                .WithMany(d => d.DoctorSpecializations)
                .HasForeignKey(ds => ds.DoctorID);

            modelBuilder.Entity<DoctorSpecialization>()
                .HasOne(ds => ds.Specialization)
                .WithMany(s => s.DoctorSpecializations)
                .HasForeignKey(ds => ds.SpecializationID);

            modelBuilder.Entity<UserInvitation>(entity =>
            {
                entity.Property(ui => ui.RecipientName).HasMaxLength(150);
                entity.Property(ui => ui.RecipientMobile).HasMaxLength(20).IsRequired();
                entity.Property(ui => ui.RecipientEmail).HasMaxLength(150);
                entity.Property(ui => ui.TokenHash).HasColumnType("varbinary(64)").IsRequired();
                entity.Property(ui => ui.Status).HasMaxLength(20).IsRequired();

                entity.HasOne(ui => ui.Hospital)
                      .WithMany(h => h.UserInvitations)
                      .HasForeignKey(ui => ui.HospitalID);

                entity.HasOne(ui => ui.Role)
                      .WithMany(r => r.UserInvitations)
                      .HasForeignKey(ui => ui.RoleID);

                entity.HasOne(ui => ui.InvitedByUser)
                      .WithMany(u => u.SentUserInvitations)
                      .HasForeignKey(ui => ui.InvitedByUserID);
            });

            // Remove invalid property configurations for FooterNote and HeaderNote
            modelBuilder.Entity<PrescriptionHeaderFooter>(entity =>
            {
                entity.Property(phf => phf.HospitalID).IsRequired();
            });

            modelBuilder.Entity<DoctorShiftOverride>(entity =>
            {
                entity.HasOne(dso => dso.Doctor)
                      .WithMany(d => d.DoctorShiftOverrides)
                      .HasForeignKey(dso => dso.DoctorID);

                entity.Property(dso => dso.OverrideDate).HasColumnType("date");
                entity.Property(dso => dso.StartDate).HasColumnType("date");
                entity.Property(dso => dso.EndDate).HasColumnType("date");
            });

            modelBuilder.Entity<DoctorTimeOff>(entity =>
            {
                entity.HasOne(dto => dto.Doctor)
                      .WithMany(d => d.DoctorTimeOffs)
                      .HasForeignKey(dto => dto.DoctorID);

                entity.Property(dto => dto.FromDate).HasColumnType("date");
                entity.Property(dto => dto.ToDate).HasColumnType("date");
            });

            // Configure DoctorQueue composite key
            modelBuilder.Entity<DoctorQueue>().ToTable("DoctorQueues");
            modelBuilder.Entity<DoctorQueue>().HasKey(dq => new { dq.HospitalId, dq.DoctorId, dq.TokenDate });

            // DoctorPreferredMedicine
            modelBuilder.Entity<DoctorPreferredMedicine>().ToTable("DoctorPreferredMedicine");
            modelBuilder.Entity<DoctorPreferredMedicine>().HasKey(e => e.PreferrredId);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.BrandName).HasMaxLength(400);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.GenericName).HasMaxLength(400).IsRequired();
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Form).HasMaxLength(100);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.StrengthValue).HasMaxLength(100);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.StrengthUnit).HasMaxLength(50);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Route).HasMaxLength(50);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Dose).HasMaxLength(100);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Frequency).HasMaxLength(50);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.DurationValue).HasMaxLength(50);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.DurationUnit).HasMaxLength(20);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Indication).HasMaxLength(400);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Notes).HasMaxLength(1000);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.MedicineId).HasMaxLength(100);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.IsActive).IsRequired();
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.CreatedBy).HasMaxLength(100).IsRequired(false);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.UpdatedAt).HasColumnType("datetime2").IsRequired(false);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.UpdatedBy).HasMaxLength(100).IsRequired(false);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.RowVersion).IsRowVersion();
            base.OnModelCreating(modelBuilder);
        }
    }
}
