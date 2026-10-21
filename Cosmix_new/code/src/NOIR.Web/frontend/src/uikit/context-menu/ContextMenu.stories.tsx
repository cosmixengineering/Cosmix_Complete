import type { Meta, StoryObj } from '@storybook/react'
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger,
  ContextMenuCheckboxItem,
  ContextMenuRadioGroup,
  ContextMenuRadioItem,
  ContextMenuLabel,
} from './ContextMenu'

const meta: Meta = {
  title: 'UIKit/ContextMenu',
  parameters: {
    docs: {
      description: {
        component: 'Right-click context menu built on Radix UI ContextMenu.',
      },
    },
  },
}
export default meta

type Story = StoryObj

export const Default: Story = {
  render: () => (
    <ContextMenu>
      <ContextMenuTrigger className="flex h-[150px] w-[300px] items-center justify-center rounded-md border border-dashed text-sm text-muted-foreground">
        Right click here
      </ContextMenuTrigger>
      <ContextMenuContent className="w-64">
        <ContextMenuItem className="cursor-pointer">
          Back <span className="ml-auto text-xs text-muted-foreground">Alt+Left</span>
        </ContextMenuItem>
        <ContextMenuItem className="cursor-pointer">
          Forward <span className="ml-auto text-xs text-muted-foreground">Alt+Right</span>
        </ContextMenuItem>
        <ContextMenuItem className="cursor-pointer">
          Reload <span className="ml-auto text-xs text-muted-foreground">Ctrl+R</span>
        </ContextMenuItem>
        <ContextMenuSeparator />
        <ContextMenuItem className="cursor-pointer">
          View Source
        </ContextMenuItem>
        <ContextMenuItem className="cursor-pointer" disabled>
          Inspect Element
        </ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  ),
}

export const WithSubmenu: Story = {
  render: () => (
    <ContextMenu>
      <ContextMenuTrigger className="flex h-[150px] w-[300px] items-center justify-center rounded-md border border-dashed text-sm text-muted-foreground">
        Right click here
      </ContextMenuTrigger>
      <ContextMenuContent className="w-64">
        <ContextMenuItem className="cursor-pointer">New Tab</ContextMenuItem>
        <ContextMenuItem className="cursor-pointer">New Window</ContextMenuItem>
        <ContextMenuSeparator />
        <ContextMenuSub>
          <ContextMenuSubTrigger className="cursor-pointer">Share</ContextMenuSubTrigger>
          <ContextMenuSubContent className="w-48">
            <ContextMenuItem className="cursor-pointer">Email</ContextMenuItem>
            <ContextMenuItem className="cursor-pointer">Message</ContextMenuItem>
            <ContextMenuItem className="cursor-pointer">Copy Link</ContextMenuItem>
          </ContextMenuSubContent>
        </ContextMenuSub>
        <ContextMenuSeparator />
        <ContextMenuItem className="cursor-pointer">Print</ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  ),
}

export const WithCheckboxAndRadio: Story = {
  render: () => (
    <ContextMenu>
      <ContextMenuTrigger className="flex h-[150px] w-[300px] items-center justify-center rounded-md border border-dashed text-sm text-muted-foreground">
        Right click here
      </ContextMenuTrigger>
      <ContextMenuContent className="w-64">
        <ContextMenuLabel>View</ContextMenuLabel>
        <ContextMenuCheckboxItem checked className="cursor-pointer">
          Show Toolbar
        </ContextMenuCheckboxItem>
        <ContextMenuCheckboxItem className="cursor-pointer">
          Show Sidebar
        </ContextMenuCheckboxItem>
        <ContextMenuSeparator />
        <ContextMenuLabel>Sort By</ContextMenuLabel>
        <ContextMenuRadioGroup value="name">
          <ContextMenuRadioItem value="name" className="cursor-pointer">Name</ContextMenuRadioItem>
          <ContextMenuRadioItem value="date" className="cursor-pointer">Date</ContextMenuRadioItem>
          <ContextMenuRadioItem value="size" className="cursor-pointer">Size</ContextMenuRadioItem>
        </ContextMenuRadioGroup>
      </ContextMenuContent>
    </ContextMenu>
  ),
}
