import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FullCalendarComponent, FullCalendarModule } from '@fullcalendar/angular';
import {
  CalendarOptions,
  DatesSetArg,
  EventClickArg,
  EventDropArg,
  EventInput,
  EventMountArg,
} from '@fullcalendar/core';
import { DateClickArg, EventResizeDoneArg } from '@fullcalendar/interaction';
import trLocale from '@fullcalendar/core/locales/tr';
import enLocale from '@fullcalendar/core/locales/en-gb';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin from '@fullcalendar/interaction';
import listPlugin from '@fullcalendar/list';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AppointmentsApiService } from '../../core/api/appointments-api.service';
import {
  AppointmentDto,
  AppointmentStatus,
  AppointmentType,
  DoctorDto,
  DoctorWorkingHourDto,
  parseUtc,
} from '../../core/api/api.models';
import { PatientOption } from '../../shared/components/patient-search-select/patient-search-select.component';
import {
  AppointmentDialogComponent,
  AppointmentDialogState,
} from './appointment-dialog.component';
import { AppointmentDetailDialogComponent } from './appointment-detail-dialog.component';
import { STATUS_COLORS, toUtcNaive, TYPE_KEYS } from './appointment-utils';

/** Randevu takvimi: FullCalendar (gun/hafta/ay/liste) + hekim filtresi + surukle-tasi. */
@Component({
  selector: 'app-calendar-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    FullCalendarModule,
    ButtonModule,
    SelectButtonModule,
    MultiSelectModule,
    ToggleSwitchModule,
    TooltipModule,
    TranslocoPipe,
    AppointmentDialogComponent,
    AppointmentDetailDialogComponent,
  ],
  templateUrl: './calendar-page.component.html',
  styleUrl: './calendar-page.component.scss',
})
export class CalendarPageComponent implements OnInit {
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly calendar = viewChild<FullCalendarComponent>('calendar');

  protected readonly AppointmentTypeEmptySlot = AppointmentType.EmptySlot;

  protected readonly doctors = signal<DoctorDto[]>([]);
  protected selectedDoctorIds: number[] = [];
  protected showCancelled = false;

  protected readonly viewTitle = signal('');
  protected activeView = 'timeGridDay';

  protected readonly dialogState = signal<AppointmentDialogState | null>(null);
  protected readonly detailAppointment = signal<AppointmentDto | null>(null);

  /** Hasta kartindan "Randevu Ver" ile gelinirse dialog on dolu acilir. */
  private pendingPatient: PatientOption | null = null;

  protected readonly viewOptions = [
    { labelKey: 'calendar.views.day', value: 'timeGridDay' },
    { labelKey: 'calendar.views.week', value: 'timeGridWeek' },
    { labelKey: 'calendar.views.month', value: 'dayGridMonth' },
    { labelKey: 'calendar.views.list', value: 'listWeek' },
  ];

  protected readonly calendarOptions: CalendarOptions = {
    plugins: [dayGridPlugin, timeGridPlugin, interactionPlugin, listPlugin],
    initialView: 'timeGridDay',
    locales: [trLocale, enLocale],
    locale: this.transloco.getActiveLang() === 'en' ? 'en-gb' : 'tr',
    headerToolbar: false,
    height: '100%',
    firstDay: 1,
    slotDuration: '00:15:00',
    slotLabelInterval: '01:00',
    slotLabelFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
    eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
    slotMinTime: '07:00:00',
    slotMaxTime: '21:00:00',
    scrollTime: '08:30:00',
    allDaySlot: false,
    nowIndicator: true,
    dayMaxEvents: true,
    selectable: true,
    selectMirror: true,
    unselectAuto: true,
    editable: true,
    events: (info, success, failure) => this.fetchEvents(info.start, info.end, success, failure),
    datesSet: (arg: DatesSetArg) => {
      this.viewTitle.set(arg.view.title);
      this.activeView = arg.view.type;
    },
    dateClick: (arg: DateClickArg) => this.onDateClick(arg),
    select: (arg) => this.openCreate({ start: arg.start, end: arg.end }),
    eventClick: (arg: EventClickArg) => this.onEventClick(arg),
    eventDrop: (arg: EventDropArg) => this.onEventChange(arg),
    eventResize: (arg: EventResizeDoneArg) => this.onEventChange(arg),
    eventDidMount: (arg: EventMountArg) => this.decorateEvent(arg),
  };

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const patientId = Number(params.get('patientId'));
    if (Number.isFinite(patientId) && patientId > 0) {
      this.pendingPatient = {
        id: patientId,
        label: params.get('patientName') ?? `#${patientId}`,
        fileNo: '',
        phone: null,
      };
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {},
        replaceUrl: true,
      });
    }

    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => {
        this.doctors.set(doctors);
        if (this.pendingPatient) {
          const patient = this.pendingPatient;
          this.pendingPatient = null;
          this.openCreate({ patient, start: this.defaultStart() });
        }
      },
      error: () => this.doctors.set([]),
    });

    this.appointmentsApi.workingHours().subscribe({
      next: (hours) => this.applyWorkingHours(hours),
      error: () => undefined,
    });
  }

  // --- Ust bar aksiyonlari --------------------------------------------------

  protected navigate(direction: 'prev' | 'next' | 'today'): void {
    const api = this.calendar()?.getApi();
    if (!api) {
      return;
    }
    if (direction === 'prev') {
      api.prev();
    } else if (direction === 'next') {
      api.next();
    } else {
      api.today();
    }
  }

  protected changeView(view: string): void {
    this.calendar()?.getApi().changeView(view);
  }

  protected refetch(): void {
    this.calendar()?.getApi().refetchEvents();
  }

  protected openCreate(
    options: {
      start?: Date;
      end?: Date;
      patient?: PatientOption | null;
      type?: AppointmentType;
    } = {},
  ): void {
    this.calendar()?.getApi().unselect();
    this.dialogState.set({
      mode: 'create',
      start: options.start ?? this.defaultStart(),
      end: options.end,
      patient: options.patient ?? null,
      type: options.type ?? AppointmentType.Normal,
      doctorUserId:
        this.selectedDoctorIds.length === 1
          ? this.selectedDoctorIds[0]
          : (this.doctors()[0]?.id ?? null),
    });
  }

  protected openEdit(appointment: AppointmentDto): void {
    this.detailAppointment.set(null);
    this.dialogState.set({ mode: 'edit', appointment });
  }

  protected onDialogSaved(): void {
    this.dialogState.set(null);
    this.refetch();
  }

  // --- FullCalendar callback'leri ------------------------------------------

  private fetchEvents(
    start: Date,
    end: Date,
    success: (events: EventInput[]) => void,
    failure: (error: Error) => void,
  ): void {
    this.appointmentsApi
      .list({
        from: toUtcNaive(start),
        to: toUtcNaive(end),
        doctorIds: this.selectedDoctorIds.length > 0 ? this.selectedDoctorIds : undefined,
      })
      .subscribe({
        next: (appointments) =>
          success(
            appointments
              .filter(
                (a) =>
                  this.showCancelled ||
                  (a.status !== AppointmentStatus.Cancelled &&
                    a.status !== AppointmentStatus.NoShow),
              )
              .map((a) => this.toEvent(a)),
          ),
        error: (error: Error) => failure(error),
      });
  }

  private toEvent(appointment: AppointmentDto): EventInput {
    const doctor = this.doctors().find((d) => d.id === appointment.doctorUserId);
    const isPlaceholder =
      appointment.type === AppointmentType.EmptySlot ||
      appointment.type === AppointmentType.Blocked;
    const background = isPlaceholder
      ? appointment.type === AppointmentType.EmptySlot
        ? '#e2e8f0'
        : '#94a3b8'
      : (appointment.color ?? doctor?.color ?? '#3b82f6');
    const classNames = ['apt'];
    if (appointment.status === AppointmentStatus.Cancelled) {
      classNames.push('apt--cancelled');
    }
    if (appointment.status === AppointmentStatus.NoShow) {
      classNames.push('apt--noshow');
    }
    if (appointment.type === AppointmentType.Control) {
      classNames.push('apt--control');
    }
    if (isPlaceholder) {
      classNames.push('apt--placeholder');
    }
    return {
      id: String(appointment.id),
      title:
        appointment.patientName ??
        appointment.title ??
        this.transloco.translate('calendar.type.' + (TYPE_KEYS[appointment.type] ?? 'normal')),
      start: parseUtc(appointment.startUtc),
      end: parseUtc(appointment.endUtc),
      backgroundColor: background,
      borderColor: background,
      textColor: isPlaceholder ? '#334155' : '#ffffff',
      editable:
        appointment.status !== AppointmentStatus.Cancelled &&
        appointment.status !== AppointmentStatus.Completed,
      classNames,
      extendedProps: { appointment },
    };
  }

  private decorateEvent(arg: EventMountArg): void {
    const appointment = arg.event.extendedProps['appointment'] as AppointmentDto | undefined;
    if (!appointment) {
      return;
    }
    const strip = STATUS_COLORS[appointment.status] ?? '#3b82f6';
    arg.el.style.borderLeft = `4px solid ${strip}`;
    arg.el.title = `${arg.event.title} · ${appointment.doctorName}`;
  }

  private onDateClick(arg: DateClickArg): void {
    // Ay gorunumunde gune tiklaninca o gunun gun gorunumune gec.
    if (this.activeView === 'dayGridMonth') {
      this.calendar()?.getApi().changeView('timeGridDay', arg.date);
      return;
    }
    const end = new Date(arg.date.getTime() + 30 * 60_000);
    this.openCreate({ start: arg.date, end });
  }

  private onEventClick(arg: EventClickArg): void {
    const appointment = arg.event.extendedProps['appointment'] as AppointmentDto | undefined;
    if (appointment) {
      this.detailAppointment.set(appointment);
    }
  }

  /** Surukle-tasi / uzat: PUT; hata olursa revert (toast error interceptor'dan gelir). */
  private onEventChange(arg: EventDropArg | EventResizeDoneArg): void {
    const appointment = arg.event.extendedProps['appointment'] as AppointmentDto | undefined;
    const start = arg.event.start;
    if (!appointment || !start) {
      arg.revert();
      return;
    }
    const end =
      arg.event.end ??
      new Date(
        start.getTime() +
          (parseUtc(appointment.endUtc).getTime() - parseUtc(appointment.startUtc).getTime()),
      );
    this.appointmentsApi
      .update(appointment.id, {
        clinicId: appointment.clinicId,
        doctorUserId: appointment.doctorUserId,
        startUtc: start.toISOString(),
        endUtc: end.toISOString(),
        type: appointment.type,
        patientId: appointment.patientId,
        title: appointment.title,
        note: appointment.note,
        color: appointment.color,
        sourceTreatmentRecordId: appointment.sourceTreatmentRecordId,
        rowVersion: appointment.rowVersion,
      })
      .subscribe({
        next: () => this.refetch(),
        error: () => arg.revert(),
      });
  }

  // --- Yardimcilar ----------------------------------------------------------

  private defaultStart(): Date {
    const start = new Date();
    start.setMinutes(Math.ceil(start.getMinutes() / 15) * 15, 0, 0);
    return start;
  }

  private applyWorkingHours(hours: DoctorWorkingHourDto[]): void {
    const api = this.calendar()?.getApi();
    if (!api || hours.length === 0) {
      return;
    }
    const businessHours = hours.map((h) => ({
      daysOfWeek: [h.dayOfWeek],
      startTime: h.startTime.slice(0, 5),
      endTime: h.endTime.slice(0, 5),
    }));
    api.setOption('businessHours', businessHours);

    const minStart = hours.reduce(
      (min, h) => (h.startTime < min ? h.startTime : min),
      hours[0].startTime,
    );
    const maxEnd = hours.reduce((max, h) => (h.endTime > max ? h.endTime : max), hours[0].endTime);
    const pad = (t: string, delta: number) => {
      const hour = Math.min(23, Math.max(0, Number(t.slice(0, 2)) + delta));
      return `${String(hour).padStart(2, '0')}:00:00`;
    };
    api.setOption('slotMinTime', pad(minStart, -1));
    api.setOption('slotMaxTime', pad(maxEnd, 1));
  }
}
