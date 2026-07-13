using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using EasyHMSAPI.Domain.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace EasyHMSAPI.Domain.Context
{
    [ExcludeFromCodeCoverage]
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Against SQL Server this call is translated straight to the real T-SQL SOUNDEX() —
        // this body never executes there. EF Core's InMemory test provider instead evaluates
        // DbFunction calls in-process, so without a real implementation here any query using
        // this predicate throws NotSupportedException under InMemory tests. Standard Soundex
        // algorithm (first letter + up to 3 digit codes, collapsing adjacent duplicate codes).
        [DbFunction("SOUNDEX", IsBuiltIn = true)]
        public static string Soundex(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            static char Code(char c) => c switch
            {
                'B' or 'F' or 'P' or 'V' => '1',
                'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => '2',
                'D' or 'T' => '3',
                'L' => '4',
                'M' or 'N' => '5',
                'R' => '6',
                _ => '0',
            };

            var letters = input.ToUpperInvariant().Where(char.IsLetter).ToArray();
            if (letters.Length == 0) return string.Empty;

            var result = new char[4];
            result[0] = letters[0];
            var resultLength = 1;
            var lastCode = Code(letters[0]);

            for (var i = 1; i < letters.Length && resultLength < 4; i++)
            {
                var code = Code(letters[i]);
                if (code != '0' && code != lastCode)
                {
                    result[resultLength++] = code;
                }
                if (letters[i] != 'H' && letters[i] != 'W')
                {
                    lastCode = code;
                }
            }

            for (; resultLength < 4; resultLength++) result[resultLength] = '0';
            return new string(result);
        }


        public DbSet<LookupType> LookupTypes { get; set; }
        public DbSet<LookupMaster> LookupMasters { get; set; }
        public DbSet<LookupPersonal> LookupPersonals { get; set; }
        public DbSet<DoctorPreferredMedicine> DoctorPreferredMedicines { get; set; }
        public DbSet<MedicineMaster> MedicineMaster { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserAuth> UserAuths { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Hospital> Hospitals { get; set; }
        public DbSet<HospitalChain> HospitalChains { get; set; }
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
        public DbSet<PrescriptionSetting> PrescriptionSettings { get; set; }
        public DbSet<DoctorSectionPreference> DoctorSectionPreferences { get; set; }
        public DbSet<DoctorPrescriptionFieldConfig> DoctorPrescriptionFieldConfigs { get; set; }
        public DbSet<DoctorDischargeFieldConfig> DoctorDischargeFieldConfigs { get; set; }
        public DbSet<DischargeSetting> DischargeSettings { get; set; }
        public DbSet<UserStatus> UserStatuses { get; set; }
        public DbSet<UserHistory> UserHistories { get; set; }
        public DbSet<PrescriptionAttachment> PrescriptionAttachments { get; set; }
        public DbSet<PrescriptionDrawing> PrescriptionDrawings { get; set; }
        public DbSet<OTPlan> OTPlans { get; set; }
        public DbSet<PackageType> PackageTypes { get; set; }
        public DbSet<OTPlanPackageType> OTPlanPackageTypes { get; set; }
        public DbSet<AdmissionReferral> AdmissionReferrals { get; set; }
        public DbSet<AdmissionReferralStatusHistory> AdmissionReferralStatusHistories { get; set; }
        public DbSet<Prescription> Prescription { get; set; }
        public DbSet<PrescriptionMedicine> PrescriptionMedicine { get; set; }
        public DbSet<PrescriptionInvestigation> PrescriptionInvestigation { get; set; }
        public DbSet<InvoicePrintSettings> InvoicePrintSettings { get; set; }
        public DbSet<BillingPolicy> BillingPolicy { get; set; }
        public DbSet<Referrer> Referrers { get; set; }
        public DbSet<NumberSeries> NumberSeries { get; set; }
        public DbSet<ChargeMaster> ChargeMaster { get; set; }
        public DbSet<BedMaster> BedMaster { get; set; }
        public DbSet<Room> Room { get; set; }
        public DbSet<Encounter> Encounter { get; set; }
        public DbSet<BillingChargeEvent> BillingChargeEvent { get; set; }
        public DbSet<BillingPayment> BillingPayment { get; set; }
        public DbSet<BillingInvoice> BillingInvoice { get; set; }
        public DbSet<BillingInvoiceChargeEvent> BillingInvoiceChargeEvent { get; set; }
        public DbSet<BillingPaymentAllocation> BillingPaymentAllocation { get; set; }
        public DbSet<BillingPaymentAllocationCharge> BillingPaymentAllocationCharge { get; set; }
        public DbSet<DiscountApproval> DiscountApproval { get; set; }
        public DbSet<CreditApproval> CreditApproval { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<DoctorFee> DoctorFees { get; set; }
        public DbSet<Alert> Alert { get; set; }
        public DbSet<Admission> Admission { get; set; }
        public DbSet<AdmissionCoverage> AdmissionCoverage { get; set; }
        public DbSet<AdmissionStatusHistory> AdmissionStatusHistory { get; set; }
        public DbSet<DischargeSummary> DischargeSummary { get; set; }
        public DbSet<DischargeMedication> DischargeMedication { get; set; }
        public DbSet<BedAssignment> BedAssignment { get; set; }
        public DbSet<ClinicalOrder> ClinicalOrder { get; set; }
        public DbSet<ClinicalOrderLine> ClinicalOrderLine { get; set; }
        public DbSet<MedicationAdministration> MedicationAdministration { get; set; }
        public DbSet<AdmissionDayBill> AdmissionDayBill { get; set; }
        public DbSet<AdmissionDayBillLine> AdmissionDayBillLine { get; set; }
        public DbSet<ConsentRecord> ConsentRecord { get; set; }
        public DbSet<ConsentTemplate> ConsentTemplate { get; set; }
        public DbSet<VitalReading> VitalReading { get; set; }
        public DbSet<FluidEntry> FluidEntry { get; set; }
        public DbSet<GlucoseReading> GlucoseReading { get; set; }
        public DbSet<NursingAssessment> NursingAssessment { get; set; }
        public DbSet<RoundNote> RoundNote { get; set; }
        public DbSet<ShiftHandoverNote> ShiftHandoverNote { get; set; }
        public DbSet<NursingCarePlanItem> NursingCarePlanItem { get; set; }
        public DbSet<RestraintOrder> RestraintOrder { get; set; }
        public DbSet<HospitalSubscription> HospitalSubscriptions { get; set; }
        public DbSet<Store> Store { get; set; }
        public DbSet<Batch> Batch { get; set; }
        public DbSet<StockLevel> StockLevel { get; set; }
        public DbSet<Vendor> Vendor { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<MaintenanceLog> MaintenanceLog { get; set; }
        public DbSet<Indent> Indent { get; set; }
        public DbSet<IndentLine> IndentLine { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrder { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLine { get; set; }
        public DbSet<GoodsReceiptNote> GoodsReceiptNote { get; set; }
        public DbSet<GoodsReceiptNoteLine> GoodsReceiptNoteLine { get; set; }
        public DbSet<NarcoticRegisterEntry> NarcoticRegisterEntry { get; set; }
        public DbSet<ColdChainTempLog> ColdChainTempLog { get; set; }
        public DbSet<InventoryItem> InventoryItem { get; set; }
        public DbSet<InventoryMovement> InventoryMovement { get; set; }
        public DbSet<BloodBag> BloodBag { get; set; }
        public DbSet<TransfusionEvent> TransfusionEvent { get; set; }
        public DbSet<OperationTheatre> OperationTheatre { get; set; }
        public DbSet<SurgeryCase> SurgeryCase { get; set; }
        public DbSet<SurgeryStatusHistory> SurgeryStatusHistory { get; set; }
        public DbSet<OTBooking> OTBooking { get; set; }
        public DbSet<PreOpAssessment> PreOpAssessment { get; set; }
        public DbSet<SurgicalSafetyChecklist> SurgicalSafetyChecklist { get; set; }
        public DbSet<IntraOpRecord> IntraOpRecord { get; set; }
        public DbSet<IntraOpItemUsage> IntraOpItemUsage { get; set; }
        public DbSet<InstrumentSet> InstrumentSet { get; set; }
        public DbSet<SterilizationCycle> SterilizationCycle { get; set; }
        public DbSet<SterilizationCycleItem> SterilizationCycleItem { get; set; }
        public DbSet<InstrumentSetMovement> InstrumentSetMovement { get; set; }
        public DbSet<IcuLevelOfCare> IcuLevelOfCare { get; set; }
        public DbSet<ApacheIIScore> ApacheIIScore { get; set; }
        public DbSet<SofaScore> SofaScore { get; set; }
        public DbSet<ChargeMasterPayerRate> ChargeMasterPayerRate { get; set; }
        public DbSet<RoomClassRateMultiplier> RoomClassRateMultiplier { get; set; }
        public DbSet<ConsultantIncentiveLedger> ConsultantIncentiveLedger { get; set; }
        public DbSet<PublicApiClient> PublicApiClient { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {   
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<UserAuth>().ToTable("UserAuth");
            modelBuilder.Entity<UserProfile>().ToTable("UserProfiles");
            
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<RolePermission>().ToTable("RolePermissions");
            modelBuilder.Entity<UserRole>().ToTable("UserRoles");

            modelBuilder.Entity<Hospital>().ToTable("Hospitals");
            modelBuilder.Entity<HospitalChain>().ToTable("HospitalChains");
            modelBuilder.Entity<HospitalUser>().ToTable("HospitalUsers");
            modelBuilder.Entity<HospitalProfileStatus>().ToTable("HospitalProfileStatus");
            modelBuilder.Entity<HospitalSetting>().ToTable("HospitalSettings");

            modelBuilder.Entity<Department>().ToTable("Departments");
            modelBuilder.Entity<HospitalDepartmentMapping>().ToTable("HospitalDepartmentMappings");
            modelBuilder.Entity<Specialization>().ToTable("Specializations");

            // Lookup entities
            modelBuilder.Entity<LookupType>().ToTable("LookupTypes");
            modelBuilder.Entity<LookupMaster>().ToTable("LookupMaster");
            modelBuilder.Entity<LookupPersonal>().ToTable("LookupPersonal");

            modelBuilder.Entity<PrescriptionAttachment>().ToTable("PrescriptionAttachment");

            // Keys and relationships for lookup entities
            modelBuilder.Entity<LookupType>().HasKey(e => e.LookupTypeId);
            modelBuilder.Entity<LookupMaster>().HasKey(e => e.LookupId);
            modelBuilder.Entity<LookupPersonal>().HasKey(e => e.PersonalId);
            modelBuilder.Entity<LookupPersonal>(entity =>
            {
                // Computed persisted column in DB: [NameLower] AS (LOWER([Name])) PERSISTED
                entity.Property(lp => lp.NameLower)
                      .HasComputedColumnSql("LOWER([Name])", stored: true);

                // Timestamp/rowversion column: prevent explicit insert/update values
                entity.Property(lp => lp.RowVersion)
                      .IsRowVersion();

                // Align column types with DB (do not force values; DB has defaults)
                entity.Property(lp => lp.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(lp => lp.ModifiedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<PrescriptionAttachment>(entity =>
            {
                entity.Property(pa => pa.RowVersion)
                      .IsRowVersion();
            });

            modelBuilder.Entity<Prescription>(entity =>             {
                entity.Property(p => p.RowVersion)
                      .IsRowVersion();
            });

            modelBuilder.Entity<PrescriptionMedicine>(entity =>
            {
                entity.Property(pm => pm.RowVersion)
                      .IsRowVersion();
            });

            modelBuilder.Entity<PrescriptionInvestigation>(entity =>
            {
                entity.Property(pi => pi.RowVersion)
                      .IsRowVersion();
            });

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

            // Configure LookupPersonal computed and generated columns
            modelBuilder.Entity<LookupPersonal>()
                .Property(lp => lp.NameLower)
                .HasComputedColumnSql(null)
                .ValueGeneratedOnAddOrUpdate();

            modelBuilder.Entity<LookupPersonal>()
                .Property(lp => lp.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<Doctor>().ToTable("Doctors");
            modelBuilder.Entity<Doctor>().HasKey(e => e.DoctorID);
            modelBuilder.Entity<Doctor>().Property(d => d.ProfileCompletionPercent).HasPrecision(5,2);
            modelBuilder.Entity<Doctor>()
                 .HasMany(d => d.DoctorDepartments)
                 .WithOne(dd => dd.Doctor)
                 .HasForeignKey(dd => dd.DoctorID);
           modelBuilder.Entity<Doctor>()
                 .HasMany(d => d.DoctorSpecializations)
                 .WithOne(ds => ds.Doctor)
                 .HasForeignKey(ds => ds.DoctorID);
            modelBuilder.Entity<DoctorDepartment>().ToTable("DoctorDepartments");
            modelBuilder.Entity<DoctorDepartment>().HasKey(e => e.DoctorDepartmentID);
            modelBuilder.Entity<DoctorDepartment>()
                 .HasOne(dd => dd.Department)
                 .WithMany(d => d.DoctorDepartments)
                 .HasForeignKey(dd => dd.DepartmentID);
            modelBuilder.Entity<DoctorDepartment>().Property(dd => dd.HospitalId).IsRequired(false);
            modelBuilder.Entity<DoctorSpecialization>().ToTable("DoctorSpecializations");
            modelBuilder.Entity<DoctorSpecialization>().HasKey(e => e.DoctorSpecializationID);
            modelBuilder.Entity<DoctorSpecialization>()
                 .HasOne(ds => ds.Specialization)
                 .WithMany(s => s.DoctorSpecializations)
                 .HasForeignKey(ds => ds.SpecializationID);
            modelBuilder.Entity<DoctorSpecialization>().Property(ds => ds.HospitalId).IsRequired(false);
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

            modelBuilder.Entity<User>().HasKey(e => e.UserID);
            modelBuilder.Entity<User>().Property(u => u.UserStatusId).IsRequired();
            modelBuilder.Entity<User>()
                 .HasOne(u => u.UserStatus)
                 .WithMany()
                 .HasForeignKey(u => u.UserStatusId);

            modelBuilder.Entity<UserAuth>().HasKey(e => e.UserAuthID);
            modelBuilder.Entity<UserAuth>().Property(ua => ua.UserStatusId).IsRequired();
            modelBuilder.Entity<UserAuth>()
                 .HasOne(ua => ua.UserStatus)
                 .WithMany()
                 .HasForeignKey(ua => ua.UserStatusId);

            modelBuilder.Entity<UserProfile>().HasKey(e => e.UserProfileID);
            modelBuilder.Entity<UserProfile>().Property(up => up.UserStatusId).IsRequired();
            modelBuilder.Entity<UserProfile>()
                 .HasOne(up => up.UserStatus)
                 .WithMany()
                 .HasForeignKey(up => up.UserStatusId);

            modelBuilder.Entity<Role>().HasKey(e => e.RoleID);
            modelBuilder.Entity<RolePermission>().HasKey(e => new { e.RoleID, e.PermissionKey });
            modelBuilder.Entity<UserRole>().HasKey(e => new { e.UserID, e.RoleID });

            modelBuilder.Entity<Hospital>().HasKey(e => e.HospitalID);
            modelBuilder.Entity<HospitalUser>().HasKey(e => e.HospitalUserID);
            modelBuilder.Entity<HospitalProfileStatus>().HasKey(e => e.HospitalID);
            modelBuilder.Entity<HospitalSetting>().HasKey(e => e.HospitalID);

            modelBuilder.Entity<Department>().HasKey(e => e.DepartmentID);
            modelBuilder.Entity<HospitalDepartmentMapping>().HasKey(e => e.MappingID);
            modelBuilder.Entity<Specialization>().HasKey(e => e.SpecializationID);


            modelBuilder.Entity<PrescriptionHeaderFooter>().HasKey(e => e.PrescriptionTemplateID);
            modelBuilder.Entity<DoctorShiftTemplate>().HasKey(e => e.TemplateID);
            modelBuilder.Entity<DoctorShiftOverride>().HasKey(e => e.OverrideID);
            modelBuilder.Entity<DoctorTimeOff>().HasKey(e => e.TimeOffID);
            modelBuilder.Entity<PrescriptionSetting>().HasKey(e => e.PrescriptionSettingId);
            modelBuilder.Entity<DischargeSetting>().HasKey(e => e.DischargeSettingId);

            // Configure PrescriptionAttachment
            modelBuilder.Entity<PrescriptionAttachment>().ToTable("PrescriptionAttachment");
            modelBuilder.Entity<PrescriptionAttachment>().HasKey(e => e.AttachmentId);
            modelBuilder.Entity<PrescriptionAttachment>().Property(e => e.UploadedAt).HasColumnType("datetime2").IsRequired(false);
            modelBuilder.Entity<PrescriptionAttachment>().Property(e => e.RowVersion).IsRowVersion();

            // Configure PrescriptionDrawing
            modelBuilder.Entity<PrescriptionDrawing>().ToTable("PrescriptionDrawing");
            modelBuilder.Entity<PrescriptionDrawing>().HasKey(e => e.DrawingId);
            modelBuilder.Entity<PrescriptionDrawing>().Property(e => e.UploadedAt).HasColumnType("datetime2").IsRequired(false);
            modelBuilder.Entity<PrescriptionDrawing>().Property(e => e.RowVersion).IsRowVersion();

            // Configure OTPlan
            modelBuilder.Entity<OTPlan>().ToTable("OTPlan");
            modelBuilder.Entity<OTPlan>().HasKey(e => e.OtPlanId);
            modelBuilder.Entity<OTPlan>().Property(e => e.CreatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<OTPlan>().Property(e => e.UpdatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<OTPlan>().Property(e => e.RowVersion).IsRowVersion();

            // Configure PackageType
            modelBuilder.Entity<PackageType>().ToTable("PackageType");
            modelBuilder.Entity<PackageType>().HasKey(e => e.PackageTypeId);
            modelBuilder.Entity<PackageType>().Property(e => e.CreatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<PackageType>().Property(e => e.UpdatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<PackageType>().Property(e => e.RowVersion).IsRowVersion();

            // Configure OTPlanPackageType (many-to-many join: an OT Plan may offer several Package Types)
            modelBuilder.Entity<OTPlanPackageType>().ToTable("OTPlanPackageType");
            modelBuilder.Entity<OTPlanPackageType>().HasKey(e => new { e.OtPlanId, e.PackageTypeId });
            modelBuilder.Entity<OTPlanPackageType>().Property(e => e.CreatedAt).HasColumnType("datetime2");

            // Configure AdmissionReferral
            modelBuilder.Entity<AdmissionReferral>().ToTable("AdmissionReferral");
            modelBuilder.Entity<AdmissionReferral>().HasKey(e => e.ReferralId);
            modelBuilder.Entity<AdmissionReferral>().Property(e => e.CreatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<AdmissionReferral>().Property(e => e.UpdatedAt).HasColumnType("datetime2");
            modelBuilder.Entity<AdmissionReferral>().Property(e => e.RowVersion).IsRowVersion();

            // Configure AdmissionReferralStatusHistory
            modelBuilder.Entity<AdmissionReferralStatusHistory>().ToTable("AdmissionReferralStatusHistory");
            modelBuilder.Entity<AdmissionReferralStatusHistory>().HasKey(e => e.HistoryId);
            modelBuilder.Entity<AdmissionReferralStatusHistory>().Property(e => e.ChangedAt).HasColumnType("datetime2");

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

            modelBuilder.Entity<HospitalChain>().HasKey(c => c.ChainId);
            modelBuilder.Entity<Hospital>()
                .HasOne(h => h.Chain)
                .WithMany(c => c.Hospitals)
                .HasForeignKey(h => h.ChainId)
                .OnDelete(DeleteBehavior.SetNull);

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
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.MedicineName).HasMaxLength(400);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.BrandName).HasMaxLength(400);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.GenericName).HasMaxLength(400).IsRequired();
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Manufacturer).HasMaxLength(200);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.DosageForm).HasMaxLength(100);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Strength).HasMaxLength(100);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Price);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Usage).HasMaxLength(500);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.SideEffects).HasMaxLength(500);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.Notes).HasMaxLength(1000);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.IsActive).IsRequired();
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.CreatedBy).HasMaxLength(100).IsRequired(false);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.UpdatedAt).HasColumnType("datetime2").IsRequired(false);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.UpdatedBy).HasMaxLength(100).IsRequired(false);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.UsageCount);
            modelBuilder.Entity<DoctorPreferredMedicine>().Property(e => e.RowVersion).IsRowVersion();

            modelBuilder.Entity<UserStatus>().ToTable("UserStatus");
            modelBuilder.Entity<UserStatus>().HasKey(us => us.UserStatusId);
            modelBuilder.Entity<UserStatus>().Property(us => us.StatusName).HasMaxLength(50).IsRequired();

            modelBuilder.Entity<UserHistory>().ToTable("UserHistory");
            modelBuilder.Entity<UserHistory>().HasKey(uh => new { uh.UserId, uh.UpdatedDate });
            modelBuilder.Entity<UserHistory>().Property(uh => uh.UserStatusId).IsRequired();
            modelBuilder.Entity<UserHistory>().Property(uh => uh.UpdatedBy).IsRequired();
            modelBuilder.Entity<UserHistory>().Property(uh => uh.UpdatedDate).HasColumnType("datetime2(3)").IsRequired();
            modelBuilder.Entity<UserHistory>()
                .HasOne(uh => uh.UserStatus)
                .WithMany(us => us.UserHistories)
                .HasForeignKey(uh => uh.UserStatusId);

            modelBuilder.Entity<Referrer>(entity =>
            {
                entity.ToTable("Referrer");
                entity.HasKey(r => r.ReferrerId);
                entity.Property(r => r.ReferrerName).HasMaxLength(200).IsRequired();
                entity.Property(r => r.ReferrerType).HasMaxLength(20).IsRequired();
                entity.Property(r => r.Phone).HasMaxLength(20);
                entity.Property(r => r.Email).HasMaxLength(120);
                entity.Property(r => r.Address).HasMaxLength(500);
                entity.Property(r => r.Pan).HasMaxLength(10);
                entity.Property(r => r.DefaultRatePercent).HasPrecision(5, 2);
                entity.Property(r => r.Notes).HasMaxLength(300);
                entity.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<NumberSeries>(entity =>
            {
                entity.ToTable("NumberSeries");
                entity.HasKey(n => n.SeriesId);
                entity.Property(n => n.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(n => n.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ChargeMaster>(entity =>
            {
                entity.ToTable("ChargeMaster");
                entity.HasKey(c => c.ChargeId);
                entity.Property(c => c.DefaultRate).HasPrecision(18, 2);
                entity.Property(c => c.DefaultQty).HasPrecision(10, 2);
                entity.Property(c => c.MaxDiscountPercent).HasPrecision(5, 2);
                entity.Property(c => c.IncentiveAmount).HasPrecision(18, 2);
                entity.Property(c => c.GstSlabPercent).HasPrecision(5, 2);
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<BedMaster>(entity =>
            {
                entity.ToTable("BedMaster");
                entity.HasKey(b => b.BedId);
                entity.Property(b => b.WardRoomDailyRate).HasPrecision(18, 2);
                entity.Property(b => b.BedDailyRateOverride).HasPrecision(18, 2);
                entity.Property(b => b.IncentiveAmount).HasPrecision(18, 2);
                entity.Property(b => b.LastStatusAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Room>(entity =>
            {
                entity.ToTable("Room");
                entity.HasKey(r => r.RoomId);
                entity.Property(r => r.DailyRate).HasPrecision(18, 2);
                entity.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Encounter>(entity =>
            {
                entity.ToTable("Encounter");
                entity.HasKey(e => e.EncounterId);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Alert>(entity =>
            {
                entity.ToTable("Alert");
                entity.HasKey(a => a.AlertId);
                entity.Property(a => a.RaisedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.DispatchedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.AcknowledgedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.DismissedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.SnoozedUntil).HasColumnType("datetime2(3)");
                entity.Property(a => a.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Admission>(entity =>
            {
                entity.ToTable("Admission");
                entity.HasKey(a => a.AdmissionId);
                entity.Property(a => a.DepositExpected).HasPrecision(18, 2);
                entity.Property(a => a.AdmittedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.ExpectedDischargeAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.DischargedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.CancelledAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<AdmissionCoverage>(entity =>
            {
                entity.ToTable("AdmissionCoverage");
                entity.HasKey(c => c.CoverageId);
                entity.Property(c => c.SanctionedAmount).HasPrecision(18, 2);
                entity.Property(c => c.ValidFrom).HasColumnType("datetime2(3)");
                entity.Property(c => c.ValidTo).HasColumnType("datetime2(3)");
                entity.Property(c => c.ClaimSubmittedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.InsurerApprovalAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.EnhancementRequestedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.EnhancedSanctionedAmount).HasPrecision(18, 2);
                entity.Property(c => c.EnhancementApprovedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<DischargeSummary>(entity =>
            {
                entity.ToTable("DischargeSummary");
                entity.HasKey(d => d.DischargeSummaryId);
                entity.Property(d => d.FollowUpDate).HasColumnType("datetime2(3)");
                entity.Property(d => d.SignedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<DischargeMedication>(entity =>
            {
                entity.ToTable("DischargeMedication");
                entity.HasKey(d => d.DischargeMedicationId);
                entity.Property(d => d.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.UpdatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<PublicApiClient>(entity =>
            {
                entity.ToTable("PublicApiClient");
                entity.HasKey(p => p.ApiClientId);
                entity.Property(p => p.LastUsedAt).HasColumnType("datetime2(3)");
                entity.Property(p => p.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(p => p.UpdatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<AdmissionStatusHistory>(entity =>
            {
                entity.ToTable("AdmissionStatusHistory");
                entity.HasKey(h => h.HistoryId);
                entity.Property(h => h.ChangedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<BedAssignment>(entity =>
            {
                entity.ToTable("BedAssignment");
                entity.HasKey(a => a.AssignmentId);
                entity.Property(a => a.DailyRateSnapshot).HasPrecision(18, 2);
                entity.Property(a => a.AssignedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.ReleasedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ClinicalOrder>(entity =>
            {
                entity.ToTable("ClinicalOrder");
                entity.HasKey(o => o.OrderId);
                entity.Property(o => o.OrderedAt).HasColumnType("datetime2(3)");
                entity.Property(o => o.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(o => o.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(o => o.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ClinicalOrderLine>(entity =>
            {
                entity.ToTable("ClinicalOrderLine");
                entity.HasKey(l => l.OrderLineId);
                entity.Property(l => l.Qty).HasPrecision(10, 2);
                entity.Property(l => l.ScheduledAt).HasColumnType("datetime2(3)");
                entity.Property(l => l.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(l => l.UpdatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<MedicationAdministration>(entity =>
            {
                entity.ToTable("MedicationAdministration");
                entity.HasKey(m => m.MedicationAdministrationId);
                entity.Property(m => m.ScheduledFor).HasColumnType("datetime2(3)");
                entity.Property(m => m.ActedAt).HasColumnType("datetime2(3)");
                entity.Property(m => m.WitnessConfirmedAt).HasColumnType("datetime2(3)");
                entity.Property(m => m.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(m => m.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ConsentRecord>(entity =>
            {
                entity.ToTable("ConsentRecord");
                entity.HasKey(c => c.ConsentRecordId);
                entity.Property(c => c.SignedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<ConsentTemplate>(entity =>
            {
                entity.ToTable("ConsentTemplate");
                entity.HasKey(c => c.ConsentTemplateId);
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<VitalReading>(entity =>
            {
                entity.ToTable("VitalReading");
                entity.HasKey(v => v.VitalReadingId);
                entity.Property(v => v.Temperature).HasPrecision(5, 2);
                entity.Property(v => v.SpO2).HasPrecision(5, 2);
                entity.Property(v => v.WeightKg).HasPrecision(6, 2);
                entity.Property(v => v.HeightCm).HasPrecision(6, 2);
                entity.Property(v => v.BMI).HasPrecision(5, 2);
                entity.Property(v => v.RecordedAt).HasColumnType("datetime2(3)");
                entity.Property(v => v.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(v => v.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(v => v.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<FluidEntry>(entity =>
            {
                entity.ToTable("FluidEntry");
                entity.HasKey(f => f.FluidEntryId);
                entity.Property(f => f.VolumeMl).HasPrecision(8, 2);
                entity.Property(f => f.RecordedAt).HasColumnType("datetime2(3)");
                entity.Property(f => f.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(f => f.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(f => f.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<GlucoseReading>(entity =>
            {
                entity.ToTable("GlucoseReading");
                entity.HasKey(g => g.GlucoseReadingId);
                entity.Property(g => g.Value).HasPrecision(6, 2);
                entity.Property(g => g.ValueMgDl).HasPrecision(6, 2);
                entity.Property(g => g.InsulinUnits).HasPrecision(5, 2);
                entity.Property(g => g.RecordedAt).HasColumnType("datetime2(3)");
                entity.Property(g => g.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(g => g.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(g => g.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<NursingAssessment>(entity =>
            {
                entity.ToTable("NursingAssessment");
                entity.HasKey(n => n.NursingAssessmentId);
                entity.Property(n => n.AssessedAt).HasColumnType("datetime2(3)");
                entity.Property(n => n.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(n => n.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(n => n.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<RoundNote>(entity =>
            {
                entity.ToTable("RoundNote");
                entity.HasKey(r => r.RoundNoteId);
                entity.Property(r => r.NotedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ShiftHandoverNote>(entity =>
            {
                entity.ToTable("ShiftHandoverNote");
                entity.HasKey(s => s.ShiftHandoverNoteId);
                entity.Property(s => s.ShiftDate).HasColumnType("date");
                entity.Property(s => s.IncomingAckAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.HandoverAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<NursingCarePlanItem>(entity =>
            {
                entity.ToTable("NursingCarePlanItem");
                entity.HasKey(n => n.CarePlanItemId);
                entity.Property(n => n.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(n => n.ResolvedAt).HasColumnType("datetime2(3)");
                entity.Property(n => n.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(n => n.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<RestraintOrder>(entity =>
            {
                entity.ToTable("RestraintOrder");
                entity.HasKey(r => r.RestraintOrderId);
                entity.Property(r => r.OrderedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.StartedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.FamilyNotifiedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.ReleasedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<BillingChargeEvent>(entity =>
            {
                entity.ToTable("BillingChargeEvent");
                entity.HasKey(e => e.ChargeEventId);
                // GrossAmount is a computed PERSISTED column in the DB.
                entity.Property(e => e.GrossAmount)
                      .ValueGeneratedOnAddOrUpdate()
                      .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
                entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
                entity.Property(e => e.NetAmount).HasPrecision(18, 2);
                entity.Property(e => e.IncentiveAmount).HasPrecision(18, 2);
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                entity.Property(e => e.Qty).HasPrecision(10, 2);
                entity.Property(e => e.HsnSacCode).HasMaxLength(10);
                entity.Property(e => e.GstRate).HasPrecision(5, 2);
                entity.Property(e => e.TaxableAmount).HasPrecision(18, 2);
                entity.Property(e => e.CgstAmount).HasPrecision(18, 2);
                entity.Property(e => e.SgstAmount).HasPrecision(18, 2);
                entity.Property(e => e.IgstAmount).HasPrecision(18, 2);
                entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
                entity.Property(e => e.IdempotencyKey).HasMaxLength(100);
                entity.Property(e => e.ServiceDate).HasColumnType("datetime2(3)");
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<BillingInvoice>(entity =>
            {
                entity.ToTable("BillingInvoice");
                entity.HasKey(e => e.InvoiceId);
                entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
                entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
                entity.Property(e => e.NetAmount).HasPrecision(18, 2);
                entity.Property(e => e.TaxableAmount).HasPrecision(18, 2);
                entity.Property(e => e.CgstAmount).HasPrecision(18, 2);
                entity.Property(e => e.SgstAmount).HasPrecision(18, 2);
                entity.Property(e => e.IgstAmount).HasPrecision(18, 2);
                entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
                entity.Property(e => e.InvoiceDate).HasColumnType("datetime2(3)");
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<BillingInvoiceChargeEvent>(entity =>
            {
                entity.ToTable("BillingInvoiceChargeEvent");
                entity.HasKey(e => new { e.InvoiceId, e.ChargeEventId });
            });

            modelBuilder.Entity<BillingPayment>(entity =>
            {
                entity.ToTable("BillingPayment");
                entity.HasKey(e => e.PaymentId);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.PaidAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<BillingPaymentAllocation>(entity =>
            {
                entity.ToTable("BillingPaymentAllocation");
                entity.HasKey(e => e.AllocationId);
                entity.Property(e => e.AllocatedAmount).HasPrecision(18, 2);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<BillingPaymentAllocationCharge>(entity =>
            {
                entity.ToTable("BillingPaymentAllocationCharge");
                entity.HasKey(e => e.AllocationChargeId);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");

                // The DB has a real FK (FK_PAYALC_Allocation) but until this was added EF's model
                // didn't know about it, so SaveChanges couldn't order batched deletes correctly —
                // it could send the BillingPaymentAllocation DELETE before this table's child rows,
                // even when the code removes children first, causing a REFERENCE constraint
                // violation (hit in CancelAppointmentHandler / VoidExistingChargesAndRefundAsync /
                // DeleteBillingEventHandler, all of which delete both in one SaveChanges call).
                // Restrict (not Cascade): still gives EF correct ordering for explicit deletes of
                // both sides, without silently auto-deleting children the code didn't ask to remove.
                entity.HasOne<BillingPaymentAllocation>()
                      .WithMany()
                      .HasForeignKey(e => e.AllocationId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DiscountApproval>(entity =>
            {
                entity.ToTable("DiscountApproval");
                entity.HasKey(d => d.DiscountApprovalId);
                entity.Property(d => d.GrossAmount).HasPrecision(18, 2);
                entity.Property(d => d.RequestedDiscountAmount).HasPrecision(18, 2);
                entity.Property(d => d.RequestedDiscountPercent).HasPrecision(5, 2);
                entity.Property(d => d.CapPercent).HasPrecision(5, 2);
                entity.Property(d => d.OverByPercent).HasPrecision(5, 2);
                entity.Property(d => d.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<CreditApproval>(entity =>
            {
                entity.ToTable("CreditApproval");
                entity.HasKey(c => c.CreditApprovalId);
                entity.Property(c => c.RequestedAmount).HasPrecision(18, 2);
                entity.Property(c => c.ResultingCreditBalance).HasPrecision(18, 2);
                entity.Property(c => c.RequestedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.DecidedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Expense>(entity =>
            {
                entity.ToTable("Expense");
                entity.HasKey(e => e.ExpenseId);
                entity.Property(e => e.ExpenseDate).HasColumnType("date");
                entity.Property(e => e.CategoryCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Vendor).HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.PaymentMode).HasMaxLength(20);
                entity.Property(e => e.StatusCode).HasMaxLength(20);
                entity.Property(e => e.ReferenceNo).HasMaxLength(100);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<DoctorFee>(entity =>
            {
                entity.ToTable("DoctorFee");
                entity.HasKey(d => d.DoctorFeeId);
                entity.Property(d => d.FeeType).HasMaxLength(30).IsRequired();
                entity.Property(d => d.Amount).HasPrecision(18, 2);
                entity.Property(d => d.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(d => d.RowVersion).IsRowVersion();
                entity.HasIndex(d => new { d.HospitalId, d.DoctorId, d.FeeType }).IsUnique();
            });

            modelBuilder.Entity<HospitalSubscription>(entity =>
            {
                entity.ToTable("HospitalSubscriptions");
                entity.HasKey(e => e.HospitalSubscriptionId);
                entity.Property(e => e.HospitalSubscriptionId).HasDefaultValueSql("newid()");
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.TrialStartDate).HasColumnType("datetime2(3)").IsRequired(false);
                entity.Property(e => e.TrialEndDate).HasColumnType("datetime2(3)").IsRequired(false);
                entity.Property(e => e.SubscriptionStartDate).HasColumnType("datetime2(3)").IsRequired(false);
                entity.Property(e => e.SubscriptionEndDate).HasColumnType("datetime2(3)").IsRequired(false);
                entity.Property(e => e.NextBillingDate).HasColumnType("datetime2(3)").IsRequired(false);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("sysutcdatetime()");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("sysutcdatetime()");

                entity.HasOne(e => e.Hospital)
                      .WithMany()
                      .HasForeignKey(e => e.HospitalId);
            });

            modelBuilder.Entity<Store>(entity =>
            {
                entity.ToTable("Store");
                entity.HasKey(s => s.StoreId);
                entity.Property(s => s.MinTempCelsius).HasPrecision(5, 2);
                entity.Property(s => s.MaxTempCelsius).HasPrecision(5, 2);
                entity.Property(s => s.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Batch>(entity =>
            {
                entity.ToTable("Batch");
                entity.HasKey(b => b.BatchId);
                entity.Property(b => b.UnitCost).HasPrecision(18, 2);
                entity.Property(b => b.ReceivedQty).HasPrecision(18, 3);
                entity.Property(b => b.RemainingQty).HasPrecision(18, 3);
                entity.Property(b => b.ManufactureDate).HasColumnType("datetime2(3)");
                entity.Property(b => b.ExpiryDate).HasColumnType("datetime2(3)");
                entity.Property(b => b.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Vendor>(entity =>
            {
                entity.ToTable("Vendor");
                entity.HasKey(v => v.VendorId);
                entity.Property(v => v.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(v => v.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(v => v.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<Indent>(entity =>
            {
                entity.ToTable("Indent");
                entity.HasKey(i => i.IndentId);
                entity.Property(i => i.RequestedAt).HasColumnType("datetime2(3)");
                entity.Property(i => i.ApprovedAt).HasColumnType("datetime2(3)");
                entity.Property(i => i.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(i => i.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(i => i.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<IndentLine>(entity =>
            {
                entity.ToTable("IndentLine");
                entity.HasKey(l => l.IndentLineId);
                entity.Property(l => l.Qty).HasPrecision(18, 3);
            });

            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.ToTable("PurchaseOrder");
                entity.HasKey(p => p.PurchaseOrderId);
                entity.Property(p => p.OrderedAt).HasColumnType("datetime2(3)");
                entity.Property(p => p.ApprovedAt).HasColumnType("datetime2(3)");
                entity.Property(p => p.ExpectedDeliveryDate).HasColumnType("datetime2(3)");
                entity.Property(p => p.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(p => p.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(p => p.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<PurchaseOrderLine>(entity =>
            {
                entity.ToTable("PurchaseOrderLine");
                entity.HasKey(l => l.PurchaseOrderLineId);
                entity.Property(l => l.Qty).HasPrecision(18, 3);
                entity.Property(l => l.Rate).HasPrecision(18, 2);
                entity.Property(l => l.ReceivedQty).HasPrecision(18, 3);
                entity.Property(l => l.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<GoodsReceiptNote>(entity =>
            {
                entity.ToTable("GoodsReceiptNote");
                entity.HasKey(g => g.GrnId);
                entity.Property(g => g.InvoiceAmount).HasPrecision(18, 2);
                entity.Property(g => g.InvoiceDate).HasColumnType("datetime2(3)");
                entity.Property(g => g.ReceivedAt).HasColumnType("datetime2(3)");
                entity.Property(g => g.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(g => g.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<GoodsReceiptNoteLine>(entity =>
            {
                entity.ToTable("GoodsReceiptNoteLine");
                entity.HasKey(l => l.GrnLineId);
                entity.Property(l => l.Qty).HasPrecision(18, 3);
                entity.Property(l => l.Rate).HasPrecision(18, 2);
                entity.Property(l => l.ManufactureDate).HasColumnType("datetime2(3)");
                entity.Property(l => l.ExpiryDate).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<Equipment>(entity =>
            {
                entity.ToTable("Equipment");
                entity.HasKey(e => e.EquipmentId);
                entity.Property(e => e.InstalledAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.WarrantyEndAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.AmcEndAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.LastServiceAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.NextDueAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(e => e.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<MaintenanceLog>(entity =>
            {
                entity.ToTable("MaintenanceLog");
                entity.HasKey(m => m.MaintenanceLogId);
                entity.Property(m => m.Cost).HasPrecision(18, 2);
                entity.Property(m => m.PerformedAt).HasColumnType("datetime2(3)");
                entity.Property(m => m.NextDueAtOverride).HasColumnType("datetime2(3)");
                entity.Property(m => m.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(m => m.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<StockLevel>(entity =>
            {
                entity.ToTable("StockLevel");
                entity.HasKey(s => s.StockLevelId);
                entity.Property(s => s.QtyOnHand).HasPrecision(18, 3);
                entity.Property(s => s.UpdatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<NarcoticRegisterEntry>(entity =>
            {
                entity.ToTable("NarcoticRegisterEntry");
                entity.HasKey(n => n.RegisterEntryId);
                entity.Property(n => n.Qty).HasPrecision(18, 3);
                entity.Property(n => n.BalanceAfter).HasPrecision(18, 3);
                entity.Property(n => n.RecordedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<ColdChainTempLog>(entity =>
            {
                entity.ToTable("ColdChainTempLog");
                entity.HasKey(c => c.LogId);
                entity.Property(c => c.TempCelsius).HasPrecision(5, 2);
                entity.Property(c => c.RecordedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.ToTable("InventoryItem");
                entity.HasKey(i => i.InventoryItemId);
                entity.Property(i => i.DefaultRate).HasPrecision(18, 2);
                entity.Property(i => i.GstSlabPercent).HasPrecision(5, 2);
                entity.Property(i => i.CurrentStock).HasPrecision(18, 3);
                entity.Property(i => i.MinStockLevel).HasPrecision(18, 3);
                entity.Property(i => i.ReorderQty).HasPrecision(18, 3);
                entity.Property(i => i.MaxStockLevel).HasPrecision(18, 3);
                entity.Property(i => i.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(i => i.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(i => i.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<InventoryMovement>(entity =>
            {
                entity.ToTable("InventoryMovement");
                entity.HasKey(m => m.InventoryMovementId);
                entity.Property(m => m.Qty).HasPrecision(18, 3);
                entity.Property(m => m.UnitCost).HasPrecision(18, 2);
                entity.Property(m => m.ExpiryDate).HasColumnType("datetime2(3)");
                entity.Property(m => m.MovedAt).HasColumnType("datetime2(3)");
                entity.Property(m => m.CreatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<BloodBag>(entity =>
            {
                entity.ToTable("BloodBag");
                entity.HasKey(b => b.BloodBagId);
                entity.Property(b => b.VolumeMl).HasPrecision(18, 2);
                entity.Property(b => b.UnitRate).HasPrecision(18, 2);
                entity.Property(b => b.GstSlabPercent).HasPrecision(5, 2);
                entity.Property(b => b.CollectedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.ExpiresAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.ReservedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.DiscardedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<TransfusionEvent>(entity =>
            {
                entity.ToTable("TransfusionEvent");
                entity.HasKey(t => t.TransfusionEventId);
                entity.Property(t => t.VolumeGivenMl).HasPrecision(18, 2);
                entity.Property(t => t.StartedAt).HasColumnType("datetime2(3)");
                entity.Property(t => t.EndedAt).HasColumnType("datetime2(3)");
                entity.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(t => t.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<OperationTheatre>(entity =>
            {
                entity.ToTable("OperationTheatre");
                entity.HasKey(t => t.TheatreId);
                entity.Property(t => t.Price).HasPrecision(18, 2);
                entity.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(t => t.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(t => t.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<SurgeryCase>(entity =>
            {
                entity.ToTable("SurgeryCase");
                entity.HasKey(s => s.SurgeryCaseId);
                entity.Property(s => s.RequestedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<SurgeryStatusHistory>(entity =>
            {
                entity.ToTable("SurgeryStatusHistory");
                entity.HasKey(h => h.HistoryId);
                entity.Property(h => h.ChangedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<OTBooking>(entity =>
            {
                entity.ToTable("OTBooking");
                entity.HasKey(b => b.OTBookingId);
                entity.Property(b => b.ScheduledStart).HasColumnType("datetime2(3)");
                entity.Property(b => b.ScheduledEnd).HasColumnType("datetime2(3)");
                entity.Property(b => b.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(b => b.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<PreOpAssessment>(entity =>
            {
                entity.ToTable("PreOpAssessment");
                entity.HasKey(p => p.PreOpAssessmentId);
                entity.Property(p => p.AssessedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<SurgicalSafetyChecklist>(entity =>
            {
                entity.ToTable("SurgicalSafetyChecklist");
                entity.HasKey(c => c.ChecklistId);
                entity.Property(c => c.SignInCompletedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.TimeOutCompletedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.SignOutCompletedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<IntraOpRecord>(entity =>
            {
                entity.ToTable("IntraOpRecord");
                entity.HasKey(r => r.IntraOpRecordId);
                entity.Property(r => r.EstimatedBloodLossMl).HasPrecision(18, 2);
                entity.Property(r => r.AnaesthesiaStartAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.AnaesthesiaEndAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.SurgeryStartAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.SurgeryEndAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RecordedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<IntraOpItemUsage>(entity =>
            {
                entity.ToTable("IntraOpItemUsage");
                entity.HasKey(u => u.IntraOpItemUsageId);
                entity.Property(u => u.Qty).HasPrecision(18, 3);
                entity.Property(u => u.UnitRate).HasPrecision(18, 2);
                entity.Property(u => u.RecordedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<InstrumentSet>(entity =>
            {
                entity.ToTable("InstrumentSet");
                entity.HasKey(s => s.InstrumentSetId);
                entity.Property(s => s.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<SterilizationCycle>(entity =>
            {
                entity.ToTable("SterilizationCycle");
                entity.HasKey(c => c.SterilizationCycleId);
                entity.Property(c => c.StartedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.EndedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<SterilizationCycleItem>(entity =>
            {
                entity.ToTable("SterilizationCycleItem");
                entity.HasKey(i => i.SterilizationCycleItemId);
            });

            modelBuilder.Entity<InstrumentSetMovement>(entity =>
            {
                entity.ToTable("InstrumentSetMovement");
                entity.HasKey(m => m.InstrumentSetMovementId);
                entity.Property(m => m.MovedAt).HasColumnType("datetime2(3)");
                entity.Property(m => m.CreatedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<IcuLevelOfCare>(entity =>
            {
                entity.ToTable("IcuLevelOfCare");
                entity.HasKey(l => l.IcuLevelOfCareId);
                entity.Property(l => l.AssessedAt).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<ApacheIIScore>(entity =>
            {
                entity.ToTable("ApacheIIScore");
                entity.HasKey(a => a.ApacheIIScoreId);
                entity.Property(a => a.Temperature).HasPrecision(5, 2);
                entity.Property(a => a.FiO2).HasPrecision(5, 2);
                entity.Property(a => a.PaO2).HasPrecision(6, 2);
                entity.Property(a => a.ArterialPh).HasPrecision(4, 2);
                entity.Property(a => a.SerumPotassium).HasPrecision(4, 2);
                entity.Property(a => a.SerumCreatinine).HasPrecision(5, 2);
                entity.Property(a => a.Hematocrit).HasPrecision(5, 2);
                entity.Property(a => a.Wbc).HasPrecision(6, 2);
                entity.Property(a => a.ScoredAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(a => a.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<SofaScore>(entity =>
            {
                entity.ToTable("SofaScore");
                entity.HasKey(s => s.SofaScoreId);
                entity.Property(s => s.PaO2FiO2Ratio).HasPrecision(6, 2);
                entity.Property(s => s.PlateletsCount).HasPrecision(8, 2);
                entity.Property(s => s.BilirubinMgDl).HasPrecision(5, 2);
                entity.Property(s => s.CreatinineMgDl).HasPrecision(5, 2);
                entity.Property(s => s.UrineOutputMlPerDay).HasPrecision(8, 2);
                entity.Property(s => s.ScoredAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(s => s.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ChargeMasterPayerRate>(entity =>
            {
                entity.ToTable("ChargeMasterPayerRate");
                entity.HasKey(r => r.ChargeMasterPayerRateId);
                entity.Property(r => r.OverrideRate).HasPrecision(18, 2);
                entity.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<RoomClassRateMultiplier>(entity =>
            {
                entity.ToTable("RoomClassRateMultiplier");
                entity.HasKey(r => r.RoomClassRateMultiplierId);
                entity.Property(r => r.MultiplierPercent).HasPrecision(6, 2);
                entity.Property(r => r.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(r => r.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ConsultantIncentiveLedger>(entity =>
            {
                entity.ToTable("ConsultantIncentiveLedger");
                entity.HasKey(c => c.ConsultantIncentiveLedgerId);
                entity.Property(c => c.IncentiveAmount).HasPrecision(18, 2);
                entity.Property(c => c.TdsAmount).HasPrecision(18, 2);
                entity.Property(c => c.AccruedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.PaidAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.CancelledAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");
                entity.Property(c => c.RowVersion).IsRowVersion();
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
