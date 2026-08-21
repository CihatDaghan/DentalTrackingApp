import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SettingsApiService } from '../../../core/api/settings-api.service';
import { RolePermissionsDto } from '../../../core/api/settings-api.models';

/** Owner rolunden alinamayan izin — arka uc de reddeder (kendini kilitlemeyi onleme). */
const LOCKED_OWNER_PERMISSION = 'settings.staff';

interface ModuleGroup {
  module: string;
  permissions: string[];
}

/**
 * Yetki matrisi: satir = izin kodu (module gore grupli), kolon = rol, hucre = checkbox.
 * Owner satirindaki `settings.staff` kilitlidir.
 */
@Component({
  selector: 'app-permission-matrix',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, CheckboxModule, TranslocoPipe],
  template: `
    <div class="flex flex-col gap-3" data-testid="permission-matrix">
      <div class="flex items-center justify-between gap-3 flex-wrap">
        <span class="text-sm text-slate-500">{{ 'settings.roles.hint' | transloco }}</span>
        <p-button
          [label]="'common.save' | transloco"
          icon="fa-solid fa-check"
          size="small"
          [loading]="saving()"
          [disabled]="!dirty()"
          (onClick)="save()"
          data-testid="roles-save"
        />
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm py-8 text-center">{{ 'common.loading' | transloco }}</p>
      } @else {
        <div class="dt-card p-0 overflow-auto">
          <table class="w-full text-sm border-collapse">
            <thead class="sticky top-0 bg-white z-10">
              <tr class="text-slate-500">
                <th class="text-left font-medium px-3 py-2 min-w-56">
                  {{ 'settings.roles.permission' | transloco }}
                </th>
                @for (role of roles(); track role.id) {
                  <th class="font-medium px-3 py-2 text-center whitespace-nowrap">
                    {{ role.name }}
                    <span class="block text-[10px] font-normal text-slate-400">
                      {{ 'settings.roles.userCount' | transloco: { count: role.userCount } }}
                    </span>
                  </th>
                }
              </tr>
            </thead>
            <tbody>
              @for (group of groups(); track group.module) {
                <tr class="bg-slate-50">
                  <td
                    class="px-3 py-1.5 font-semibold text-slate-600 text-xs uppercase tracking-wide"
                    [attr.colspan]="roles().length + 1"
                  >
                    {{ 'settings.roles.modules.' + group.module | transloco }}
                  </td>
                </tr>
                @for (permission of group.permissions; track permission) {
                  <tr class="border-t border-slate-100">
                    <td class="px-3 py-1.5 text-slate-700 font-mono text-xs">{{ permission }}</td>
                    @for (role of roles(); track role.id) {
                      <td class="px-3 py-1.5 text-center">
                        <p-checkbox
                          [binary]="true"
                          [ngModel]="has(role.id, permission)"
                          (ngModelChange)="toggle(role.id, permission, $event)"
                          [disabled]="isLocked(role, permission)"
                          [attr.data-testid]="'perm-' + role.id + '-' + permission"
                        />
                      </td>
                    }
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class PermissionMatrixComponent implements OnInit {
  private readonly api = inject(SettingsApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly roles = signal<RolePermissionsDto[]>([]);
  protected readonly groups = signal<ModuleGroup[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  /** roleId -> izin seti (duzenleme tamponu). */
  private readonly draft = signal<Record<number, Set<string>>>({});
  private original: Record<number, Set<string>> = {};

  protected readonly dirty = computed(() => {
    const draft = this.draft();
    return Object.entries(draft).some(([roleId, set]) => {
      const base = this.original[Number(roleId)];
      if (!base || base.size !== set.size) {
        return true;
      }
      for (const p of set) {
        if (!base.has(p)) {
          return true;
        }
      }
      return false;
    });
  });

  ngOnInit(): void {
    forkJoin({ roles: this.api.roles(), catalog: this.api.permissionCatalog() }).subscribe({
      next: ({ roles, catalog }) => {
        this.roles.set(roles);
        this.groups.set(
          Object.entries(catalog.byModule).map(([module, permissions]) => ({ module, permissions })),
        );
        const draft: Record<number, Set<string>> = {};
        this.original = {};
        for (const role of roles) {
          draft[role.id] = new Set(role.permissions);
          this.original[role.id] = new Set(role.permissions);
        }
        this.draft.set(draft);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected has(roleId: number, permission: string): boolean {
    return this.draft()[roleId]?.has(permission) ?? false;
  }

  /** Owner rolunun `settings.staff` izni kaldirilamaz. */
  protected isLocked(role: RolePermissionsDto, permission: string): boolean {
    return role.name === 'Owner' && permission === LOCKED_OWNER_PERMISSION;
  }

  protected toggle(roleId: number, permission: string, checked: boolean): void {
    this.draft.update((draft) => {
      const next = { ...draft };
      const set = new Set(next[roleId] ?? []);
      if (checked) {
        set.add(permission);
      } else {
        set.delete(permission);
      }
      next[roleId] = set;
      return next;
    });
  }

  protected save(): void {
    const draft = this.draft();
    const changed = this.roles().filter((role) => {
      const base = this.original[role.id];
      const set = draft[role.id];
      if (!base || !set || base.size !== set.size) {
        return true;
      }
      for (const p of set) {
        if (!base.has(p)) {
          return true;
        }
      }
      return false;
    });
    if (changed.length === 0) {
      return;
    }
    this.saving.set(true);
    forkJoin(
      changed.map((role) =>
        this.api.updateRolePermissions(role.id, [...(draft[role.id] ?? [])].sort()),
      ),
    ).subscribe({
      next: (updated) => {
        this.roles.update((roles) =>
          roles.map((r) => updated.find((u) => u.id === r.id) ?? r),
        );
        for (const role of updated) {
          this.original[role.id] = new Set(role.permissions);
        }
        this.draft.update((d) => {
          const next = { ...d };
          for (const role of updated) {
            next[role.id] = new Set(role.permissions);
          }
          return next;
        });
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('settings.saved'),
          life: 3000,
        });
      },
      error: () => this.saving.set(false),
    });
  }
}
