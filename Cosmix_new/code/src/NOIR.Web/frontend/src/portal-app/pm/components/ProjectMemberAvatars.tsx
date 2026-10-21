import { useTranslation } from 'react-i18next'
import { Avatar, Tooltip, TooltipContent, TooltipTrigger } from '@uikit'
import type { ProjectMemberDto } from '@/types/pm'

const MAX_VISIBLE = 5

interface ProjectMemberAvatarsProps {
  members: ProjectMemberDto[]
  onClickMore?: () => void
}

export const ProjectMemberAvatars = ({ members, onClickMore }: ProjectMemberAvatarsProps) => {
  const { t } = useTranslation('common')

  const visible = members.slice(0, MAX_VISIBLE)
  const overflowCount = members.length - MAX_VISIBLE

  return (
    <div className="flex items-center">
      {visible.map((member, index) => (
        <Tooltip key={member.id}>
          <TooltipTrigger asChild>
            <span
              className={`relative inline-block ring-2 ring-background rounded-full ${index > 0 ? '-ml-2' : ''}`}
              style={{ zIndex: MAX_VISIBLE - index }}
            >
              <Avatar
                size="sm"
                src={member.avatarUrl ?? undefined}
                alt={member.employeeName}
                fallback={member.employeeName}
              />
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <p>{member.employeeName}</p>
            <p className="text-xs opacity-70">{t(`memberRoles.${member.role.toLowerCase()}`)}</p>
          </TooltipContent>
        </Tooltip>
      ))}
      {overflowCount > 0 && (
        <button
          type="button"
          className="-ml-2 relative inline-flex h-8 w-8 items-center justify-center rounded-full bg-muted text-xs font-medium ring-2 ring-background cursor-pointer hover:bg-muted/80 transition-colors"
          style={{ zIndex: 0 }}
          onClick={onClickMore}
          aria-label={t('pm.moreMembers', { count: overflowCount })}
        >
          +{overflowCount}
        </button>
      )}
    </div>
  )
}
